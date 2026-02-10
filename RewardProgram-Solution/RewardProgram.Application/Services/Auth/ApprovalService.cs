using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;
using System.Security.Cryptography;

namespace RewardProgram.Application.Services.Auth;

public class ApprovalService : IApprovalService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ITwilioService _twilioService;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        ITwilioService twilioService,
        ILogger<ApprovalService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _twilioService = twilioService;
        _logger = logger;
    }

    public async Task<Result<PaginatedResult<PendingUserResponse>>> GetPendingRequestsAsync(string approverId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure<PaginatedResult<PendingUserResponse>>(AuthErrors.UserNotFound);

        var roles = await _userRepository.GetRolesAsync(approver);

        IQueryable<ApplicationUser> query;

        if (roles.Contains(UserRoles.SalesMan))
        {
            query = _userRepository.Query()
                .Where(u => u.AssignedSalesManId == approverId
                         && u.RegistrationStatus == RegistrationStatus.PendingSalesman);
        }
        else if (roles.Contains(UserRoles.ZoneManager))
        {
            query = _userRepository.Query()
                .Where(u => u.RegistrationStatus == RegistrationStatus.PendingZoneManager
                         && u.AssignedSalesMan != null
                         && u.AssignedSalesMan.ZoneManagerId == approverId);
        }
        else
        {
            return Result.Failure<PaginatedResult<PendingUserResponse>>(ApprovalErrors.NotAuthorizedToApprove);
        }

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.ShopOwnerProfile)
            .Include(u => u.SellerProfile)
                .ThenInclude(sp => sp!.ShopOwner)
                    .ThenInclude(so => so.User)
            .Include(u => u.AssignedSalesMan)
            .ToListAsync(ct);

        // Resolve city/district names in bulk
        var cityIds = users
            .Where(u => u.NationalAddress != null)
            .Select(u => u.NationalAddress!.CityId)
            .Distinct()
            .ToList();

        var districtIds = users
            .Where(u => u.NationalAddress != null)
            .Select(u => u.NationalAddress!.DistrictId)
            .Distinct()
            .ToList();

        var cities = await _context.Cities
            .Where(c => cityIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr, ct);

        var districts = await _context.Districts
            .Where(d => districtIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { d.NameAr, d.Zone }, ct);

        var items = users.Select(u =>
        {
            string? cityName = null;
            string? districtName = null;
            Zone? zone = null;

            if (u.NationalAddress != null)
            {
                cities.TryGetValue(u.NationalAddress.CityId, out cityName);
                if (districts.TryGetValue(u.NationalAddress.DistrictId, out var districtInfo))
                {
                    districtName = districtInfo.NameAr;
                    zone = districtInfo.Zone;
                }
            }

            return new PendingUserResponse(
                Id: u.Id,
                Name: u.Name,
                MobileNumber: u.MobileNumber,
                UserType: u.UserType,
                RegistrationStatus: u.RegistrationStatus,
                RegisteredAt: u.CreatedAt,
                StoreName: u.ShopOwnerProfile?.StoreName,
                VAT: u.ShopOwnerProfile?.VAT,
                CRN: u.ShopOwnerProfile?.CRN,
                ShopImageUrl: u.ShopOwnerProfile?.ShopImageUrl,
                ShopCode: u.ShopOwnerProfile?.ShopCode,
                CityName: cityName,
                DistrictName: districtName,
                Zone: zone,
                Street: u.NationalAddress?.Street,
                BuildingNumber: u.NationalAddress?.BuildingNumber,
                PostalCode: u.NationalAddress?.PostalCode,
                SubNumber: u.NationalAddress?.SubNumber,
                ShopOwnerName: u.SellerProfile?.ShopOwner?.User?.Name,
                AssignedSalesManName: u.AssignedSalesMan?.Name
            );
        }).ToList();

        var result = new PaginatedResult<PendingUserResponse>(items, totalCount, page, pageSize);
        return Result.Success(result);
    }

    public async Task<Result> ApproveAsync(string userId, string approverId, CancellationToken ct = default)
    {
        var user = await _userRepository.Query()
            .Include(u => u.ShopOwnerProfile)
            .Include(u => u.AssignedSalesMan)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var roles = await _userRepository.GetRolesAsync(approver);

        // SalesMan approval: PendingSalesman → PendingZoneManager
        if (user.RegistrationStatus == RegistrationStatus.PendingSalesman
            && roles.Contains(UserRoles.SalesMan))
        {
            if (user.AssignedSalesManId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);

            if (string.IsNullOrEmpty(approver.ZoneManagerId))
                return Result.Failure(ApprovalErrors.SalesManHasNoZoneManager);

            var fromStatus = user.RegistrationStatus;
            user.RegistrationStatus = RegistrationStatus.PendingZoneManager;

            await _userRepository.UpdateAsync(user);
            await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Approved, fromStatus, user.RegistrationStatus, ct: ct);

            _logger.LogInformation(
                "SalesMan {ApproverId} approved user {UserId}. Status: PendingSalesman → PendingZoneManager",
                approverId, userId);

            return Result.Success();
        }

        // ZoneManager approval: PendingZoneManager → Approved
        if (user.RegistrationStatus == RegistrationStatus.PendingZoneManager
            && roles.Contains(UserRoles.ZoneManager))
        {
            if (user.AssignedSalesMan?.ZoneManagerId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);

            var fromStatus = user.RegistrationStatus;
            user.RegistrationStatus = RegistrationStatus.Approved;

            if (user.ShopOwnerProfile != null)
            {
                var shopCode = await GenerateShopCodeAsync(ct: ct);
                if (shopCode is null)
                    return Result.Failure(ApprovalErrors.ShopCodeGenerationFailed);

                user.ShopOwnerProfile.ShopCode = shopCode;
            }

            await _userRepository.UpdateAsync(user);
            await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Approved, fromStatus, user.RegistrationStatus, ct: ct);

            _logger.LogInformation(
                "ZoneManager {ApproverId} approved user {UserId}. Status: PendingZoneManager → Approved. ShopCode: {ShopCode}",
                approverId, userId, user.ShopOwnerProfile?.ShopCode);

            // Send WhatsApp welcome (fire-and-forget with captured data to avoid disposed-context issues)
            var welcomeMobile = user.MobileNumber;
            var welcomeName = user.Name;
            var welcomeUserType = user.UserType;
            var welcomeShopCode = user.ShopOwnerProfile?.ShopCode;
            _ = SendWelcomeMessageAsync(welcomeMobile, welcomeName, welcomeUserType, welcomeShopCode);

            return Result.Success();
        }

        if (user.RegistrationStatus != RegistrationStatus.PendingSalesman
            && user.RegistrationStatus != RegistrationStatus.PendingZoneManager)
        {
            return Result.Failure(ApprovalErrors.UserNotPendingApproval);
        }

        return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);
    }

    public async Task<Result> RejectAsync(string userId, string reason, string approverId, CancellationToken ct = default)
    {
        var user = await _userRepository.Query()
            .Include(u => u.AssignedSalesMan)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var roles = await _userRepository.GetRolesAsync(approver);

        // Validate authorization
        if (user.RegistrationStatus == RegistrationStatus.PendingSalesman
            && roles.Contains(UserRoles.SalesMan))
        {
            if (user.AssignedSalesManId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);
        }
        else if (user.RegistrationStatus == RegistrationStatus.PendingZoneManager
                 && roles.Contains(UserRoles.ZoneManager))
        {
            if (user.AssignedSalesMan?.ZoneManagerId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);
        }
        else if (user.RegistrationStatus != RegistrationStatus.PendingSalesman
                 && user.RegistrationStatus != RegistrationStatus.PendingZoneManager)
        {
            return Result.Failure(ApprovalErrors.UserNotPendingApproval);
        }
        else
        {
            return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);
        }

        var fromStatus = user.RegistrationStatus;
        user.RegistrationStatus = RegistrationStatus.Rejected;

        await _userRepository.UpdateAsync(user);
        await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Rejected, fromStatus, RegistrationStatus.Rejected, reason, ct);

        _logger.LogInformation(
            "Approver {ApproverId} rejected user {UserId}. Status: {FromStatus} → Rejected. Reason: {Reason}",
            approverId, userId, fromStatus, reason);

        return Result.Success();
    }

    #region Private Helpers

    private async Task<string?> GenerateShopCodeAsync(int maxAttempts = 10, CancellationToken ct = default)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            var code = GenerateRandomCode(6);
            var exists = await _context.ShopOwnerProfiles.AnyAsync(p => p.ShopCode == code, ct);
            if (!exists)
                return code;
        }

        _logger.LogError("Failed to generate unique ShopCode after {MaxAttempts} attempts", maxAttempts);
        return null;
    }

    private static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return RandomNumberGenerator.GetString(chars, length);
    }

    private async Task LogApprovalRecordAsync(
        string userId,
        string approverId,
        ApprovalAction action,
        RegistrationStatus fromStatus,
        RegistrationStatus toStatus,
        string? rejectionReason = null,
        CancellationToken ct = default)
    {
        var record = new ApprovalRecord
        {
            UserId = userId,
            ApproverId = approverId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            RejectionReason = rejectionReason,
            CreatedBy = approverId
        };

        _context.ApprovalRecords.Add(record);
        await _context.SaveChangesAsync(ct);
    }

    private async Task SendWelcomeMessageAsync(string mobileNumber, string userName, UserType userType, string? shopCode)
    {
        try
        {
            var message = userType == UserType.ShopOwner
                ? $"مرحباً {userName}! تمت الموافقة على تسجيلك في برنامج المكافآت. كود المحل الخاص بك: {shopCode}"
                : $"مرحباً {userName}! تمت الموافقة على طلبك، يمكنك الآن تسجيل الدخول";

            await _twilioService.SendWhatsAppMessageAsync(mobileNumber, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome WhatsApp message to {Mobile}", MobileNumberHelper.Mask(mobileNumber));
        }
    }

    #endregion
}
