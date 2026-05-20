using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.Accounts;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Admin;

/// <summary>
/// Manages Admin-role dashboard accounts and their permissions. SystemAdmin
/// accounts are read-only here: they appear in the list but cannot be modified
/// or deleted, which also makes locking the dashboard out impossible.
/// </summary>
public class AdminAccountService : IAdminAccountService
{
    private readonly IUserRepository _userRepository;
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<ErrorMessages> _localizer;
    private readonly ILogger<AdminAccountService> _logger;

    public AdminAccountService(
        IUserRepository userRepository,
        IApplicationDbContext context,
        IStringLocalizer<ErrorMessages> localizer,
        ILogger<AdminAccountService> logger)
    {
        _userRepository = userRepository;
        _context = context;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<Result<List<AdminAccountListItem>>> ListAsync(CancellationToken ct = default)
    {
        var admins = await _userRepository.GetUsersInRoleAsync(UserRoles.Admin);
        var systemAdmins = await _userRepository.GetUsersInRoleAsync(UserRoles.SystemAdmin);

        var systemAdminIds = systemAdmins.Select(u => u.Id).ToHashSet();
        var all = systemAdmins
            .Concat(admins.Where(a => !systemAdminIds.Contains(a.Id)))
            .ToList();

        var ids = all.Select(u => u.Id).ToList();
        var permissionCounts = await _context.AdminUserPermissions
            .Where(p => ids.Contains(p.UserId))
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var items = all
            .Select(u => new AdminAccountListItem(
                u.Id,
                u.UserName ?? string.Empty,
                u.Name,
                systemAdminIds.Contains(u.Id),
                u.IsDisabled,
                permissionCounts.GetValueOrDefault(u.Id),
                u.CreatedAt))
            .OrderByDescending(x => x.IsSystemAdmin)
            .ThenBy(x => x.Name)
            .ToList();

        return Result.Success(items);
    }

    public async Task<Result<AdminAccountDetailResponse>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(id, ct);
        if (user is null)
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.NotFound);

        var roles = await _userRepository.GetRolesAsync(user);
        var isSystemAdmin = roles.Contains(UserRoles.SystemAdmin);
        if (!isSystemAdmin && !roles.Contains(UserRoles.Admin))
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.NotFound);

        return Result.Success(await BuildDetailAsync(user, isSystemAdmin, ct));
    }

    public async Task<Result<AdminAccountDetailResponse>> CreateAsync(
        CreateAdminAccountRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();

        var existing = await _userRepository.FindByUsernameAsync(username);
        if (existing is not null)
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.UsernameAlreadyExists);

        var user = new ApplicationUser
        {
            UserName = username,
            Name = request.Name.Trim(),
            // ApplicationUser.MobileNumber is required and unique-indexed; admin
            // accounts have no real mobile, so use a unique synthetic value.
            MobileNumber = "adm" + Guid.NewGuid().ToString("N")[..12],
            // UserType.SystemAdmin tags the account as an admin-dashboard user, so
            // it is excluded from the SalesMan/ZoneManager user list. The Admin
            // ROLE (added below) — not the UserType — is what limits its access.
            UserType = UserType.SystemAdmin,
            RegistrationStatus = RegistrationStatus.Approved
        };

        await using var transaction = await _context.BeginTransactionAsync(ct);

        var createResult = await _userRepository.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Admin account creation failed for '{Username}': {Errors}",
                username, string.Join("; ", createResult.Errors.Select(e => e.Description)));
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.CreateFailed);
        }

        var roleResult = await _userRepository.AddToRoleAsync(user, UserRoles.Admin);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.CreateFailed);
        }

        await transaction.CommitAsync(ct);
        _logger.LogInformation("Admin account '{Username}' ({UserId}) created", username, user.Id);

        // A new admin starts with no permissions — least privilege.
        return Result.Success(new AdminAccountDetailResponse(
            user.Id, username, user.Name, false, user.IsDisabled, user.CreatedAt, []));
    }

    public async Task<Result<AdminAccountDetailResponse>> UpdateAsync(
        string id, UpdateAdminAccountRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(id, ct);
        if (user is null)
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.NotFound);

        var guard = await EnsureEditableAdminAsync(user);
        if (guard.IsFailure)
            return Result.Failure<AdminAccountDetailResponse>(guard.Error);

        user.Name = request.Name.Trim();
        user.IsDisabled = request.IsDisabled;

        var updateResult = await _userRepository.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.UpdateFailed);

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            var passwordResult = await _userRepository.SetPasswordAsync(user, request.NewPassword);
            if (!passwordResult.Succeeded)
                return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.UpdateFailed);
        }

        // A disabled admin must lose any live session immediately: kill refresh
        // tokens and bump the security stamp so existing access tokens are rejected.
        if (request.IsDisabled)
        {
            await _userRepository.RevokeAllRefreshTokensAsync(user.Id, ct);
            await _userRepository.UpdateSecurityStampAsync(user);
        }

        _logger.LogInformation("Admin account {UserId} updated", user.Id);
        return Result.Success(await BuildDetailAsync(user, isSystemAdmin: false, ct));
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(id, ct);
        if (user is null)
            return Result.Failure(AdminAccountErrors.NotFound);

        var guard = await EnsureEditableAdminAsync(user);
        if (guard.IsFailure)
            return guard;

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var permissions = await _context.AdminUserPermissions
                .Where(p => p.UserId == id)
                .ToListAsync(ct);
            if (permissions.Count > 0)
            {
                _context.AdminUserPermissions.RemoveRange(permissions);
                await _context.SaveChangesAsync(ct);
            }

            // The global cascade-to-Restrict override (ApplicationDbContext.
            // OnModelCreating) disables cascade on the Identity AspNetUserRoles FK,
            // so those rows must be removed explicitly or they block the delete.
            var userRoles = await _userRepository.GetRolesAsync(user);
            if (userRoles.Count > 0)
            {
                var removeRolesResult = await _userRepository.RemoveFromRolesAsync(user, userRoles);
                if (!removeRolesResult.Succeeded)
                {
                    await transaction.RollbackAsync(ct);
                    return Result.Failure(AdminAccountErrors.UpdateFailed);
                }
            }

            var deleteResult = await _userRepository.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure(AdminAccountErrors.UpdateFailed);
            }

            await transaction.CommitAsync(ct);
            _logger.LogInformation("Admin account {UserId} deleted", id);
            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            // FK references (e.g. redemption approvals authored by this admin)
            // block a hard delete — the account should be disabled instead.
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(ex, "Admin account {UserId} could not be deleted — activity history exists", id);
            return Result.Failure(AdminAccountErrors.HasHistory);
        }
    }

    public async Task<Result<AdminAccountDetailResponse>> SetPermissionsAsync(
        string id, SetAdminPermissionsRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(id, ct);
        if (user is null)
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.NotFound);

        var guard = await EnsureEditableAdminAsync(user);
        if (guard.IsFailure)
            return Result.Failure<AdminAccountDetailResponse>(guard.Error);

        var requested = request.Permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (requested.Any(p => !AdminPermissions.IsValid(p)))
            return Result.Failure<AdminAccountDetailResponse>(AdminAccountErrors.InvalidPermission);

        // Replace the whole set.
        var existing = await _context.AdminUserPermissions
            .Where(p => p.UserId == id)
            .ToListAsync(ct);
        _context.AdminUserPermissions.RemoveRange(existing);

        foreach (var permission in requested)
        {
            await _context.AdminUserPermissions.AddAsync(new AdminUserPermission
            {
                UserId = id,
                Permission = permission
            }, ct);
        }
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Admin account {UserId} permissions set ({Count})", id, requested.Count);
        return Result.Success(await BuildDetailAsync(user, isSystemAdmin: false, ct));
    }

    public Result<List<PermissionCatalogModule>> GetPermissionCatalog()
    {
        var catalog = AdminPermissions.All
            .Select(key => new { Key = key, Parts = key.Split('.', 2) })
            .GroupBy(x => x.Parts[0])
            .Select(g => new PermissionCatalogModule(
                g.Key,
                _localizer[$"Perm.Module.{g.Key}"],
                g.Select(x => new PermissionCatalogItem(
                    x.Key,
                    _localizer[$"Perm.Action.{x.Parts[1]}"]))
                    .ToList()))
            .ToList();

        return Result.Success(catalog);
    }

    // Loads roles and rejects the request when the target is not an editable
    // Admin account — SystemAdmin accounts are immutable here, and any other
    // user id is treated as not found.
    private async Task<Result> EnsureEditableAdminAsync(ApplicationUser user)
    {
        var roles = await _userRepository.GetRolesAsync(user);
        if (roles.Contains(UserRoles.SystemAdmin))
            return Result.Failure(AdminAccountErrors.CannotModifySystemAdmin);
        if (!roles.Contains(UserRoles.Admin))
            return Result.Failure(AdminAccountErrors.NotFound);
        return Result.Success();
    }

    private async Task<AdminAccountDetailResponse> BuildDetailAsync(
        ApplicationUser user, bool isSystemAdmin, CancellationToken ct)
    {
        var permissions = await _context.AdminUserPermissions
            .Where(p => p.UserId == user.Id)
            .Select(p => p.Permission)
            .ToListAsync(ct);

        return new AdminAccountDetailResponse(
            user.Id,
            user.UserName ?? string.Empty,
            user.Name,
            isSystemAdmin,
            user.IsDisabled,
            user.CreatedAt,
            permissions);
    }
}
