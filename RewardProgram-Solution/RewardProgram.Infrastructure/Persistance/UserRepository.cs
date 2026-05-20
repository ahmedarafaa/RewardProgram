using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Infrastructure.Persistance;

public class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    public UserRepository(UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct = default)
        => await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task<ApplicationUser?> FindByMobileAsync(string mobileNumber, CancellationToken ct = default)
        => await _userManager.Users.FirstOrDefaultAsync(u => u.MobileNumber == mobileNumber, ct);

    public async Task<ApplicationUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => await _userManager.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken), ct);

    public async Task<bool> IsMobileBlockedAsync(string mobileNumber, CancellationToken ct = default)
        => await _userManager.Users.AnyAsync(
            u => u.MobileNumber == mobileNumber
              && u.RegistrationStatus != RegistrationStatus.Rejected,
            ct);

    public async Task ArchiveRejectedUserByMobileAsync(string mobileNumber, CancellationToken ct = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(
            u => u.MobileNumber == mobileNumber
              && u.RegistrationStatus == RegistrationStatus.Rejected,
            ct);

        if (user is null)
            return;

        // Ticks make the prefix collision-free across repeat-rejection cycles.
        var prefix = $"DEL_{DateTime.UtcNow.Ticks}_";

        user.MobileNumber = prefix + user.MobileNumber;
        user.UserName = prefix + (user.UserName ?? string.Empty);
        user.NormalizedUserName = _userManager.NormalizeName(user.UserName);
        user.PhoneNumber = string.IsNullOrEmpty(user.PhoneNumber)
            ? user.PhoneNumber
            : prefix + user.PhoneNumber;

        user.IsAccountDeleted = true;
        user.IsDisabled = true;
        user.FcmToken = null;
        user.SecurityStamp = Guid.NewGuid().ToString();

        await _userManager.UpdateAsync(user);
        await RevokeAllRefreshTokensAsync(user.Id, ct);
    }

    public IQueryable<ApplicationUser> Query() => _userManager.Users;

    public Task<IdentityResult> CreateAsync(ApplicationUser user) => _userManager.CreateAsync(user);

    public Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
        => _userManager.CreateAsync(user, password);

    public async Task<ApplicationUser?> FindByUsernameAsync(string username)
        => await _userManager.FindByNameAsync(username);

    public async Task<IdentityResult> SetPasswordAsync(ApplicationUser user, string newPassword)
    {
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
            return removeResult;

        return await _userManager.AddPasswordAsync(user, newPassword);
    }

    public Task<IdentityResult> DeleteAsync(ApplicationUser user) => _userManager.DeleteAsync(user);

    public Task<IdentityResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
        => _userManager.RemoveFromRolesAsync(user, roles);

    public Task<IdentityResult> UpdateAsync(ApplicationUser user) => _userManager.UpdateAsync(user);

    public Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role) => _userManager.AddToRoleAsync(user, role);

    public Task<IList<string>> GetRolesAsync(ApplicationUser user) => _userManager.GetRolesAsync(user);

    public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName) => _userManager.GetUsersInRoleAsync(roleName);

    public Task<IdentityResult> UpdateSecurityStampAsync(ApplicationUser user)
        => _userManager.UpdateSecurityStampAsync(user);

    public async Task<int> RevokeAllRefreshTokensAsync(string userId, CancellationToken ct = default)
    {
        // RefreshTokens is an owned collection of ApplicationUser; underlying table
        // "RefreshTokens" with composite key (UserId, Token). Bulk-update via raw
        // SQL avoids loading the user entity (which without an explicit Include
        // returns an empty navigation anyway) and avoids round-tripping through
        // UserManager.UpdateAsync.
        var revokedAt = DateTime.UtcNow;
        return await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE RefreshTokens SET RevokedOn = {revokedAt} WHERE UserId = {userId} AND RevokedOn IS NULL",
            ct);
    }
}
