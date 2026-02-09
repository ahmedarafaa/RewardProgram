using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Errors;
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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITwilioRepository _twilioRepository;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ITwilioRepository twilioRepository,
        ILogger<ApprovalService> logger)
    {
        _context = context;
        _userManager = userManager;
        _twilioRepository = twilioRepository;
        _logger = logger;
    }

    public async Task<Result<List<PendingUserResponse>>> GetPendingRequestsAsync(string approverId)
    {
        var approver = await _userManager.FindByIdAsync(approverId);
        if (approver is null)
            return Result.Failure<List<PendingUserResponse>>(AuthErrors.UserNotFound);

        var roles = await _userManager.GetRolesAsync(approver);

        IQueryable<ApplicationUser> query;

        if (roles.Contains(UserRoles.SalesMan))
        {
            // SalesMan sees users assigned to them with PendingSalesman status
            query = _userManager.Users
                .Where(u => u.AssignedSalesManId == approverId
                         && u.RegistrationStatus == RegistrationStatus.PendingSalesman);
        }
        else if (roles.Contains(UserRoles.ZoneManager))
        {
            // ZoneManager sees users whose assigned salesman reports to them, with PendingZoneManager status
            query = _userManager.Users
                .Where(u => u.RegistrationStatus == RegistrationStatus.PendingZoneManager
                         && u.AssignedSalesMan != null
                         && u.AssignedSalesMan.ZoneManagerId == approverId);
        }
        else
        {
            return Result.Failure<List<PendingUserResponse>>(ApprovalErrors.NotAuthorizedToApprove);
        }

        var users = await query
            .Include(u => u.ShopOwnerProfile)
            .Include(u => u.AssignedSalesMan)
            .ToListAsync();

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
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var districts = await _context.Districts
            .Where(d => districtIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { d.NameAr, d.Zone });

        var result = users.Select(u =>
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
                AssignedSalesManName: u.AssignedSalesMan?.Name
            );
        }).ToList();

        return Result.Success(result);
    }

    public async Task<Result> ApproveAsync(string userId, string approverId)
    {
        var user = await _userManager.Users
            .Include(u => u.ShopOwnerProfile)
            .Include(u => u.AssignedSalesMan)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var approver = await _userManager.FindByIdAsync(approverId);
        if (approver is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var roles = await _userManager.GetRolesAsync(approver);

        // SalesMan approval: PendingSalesman → PendingZoneManager
        if (user.RegistrationStatus == RegistrationStatus.PendingSalesman
            && roles.Contains(UserRoles.SalesMan))
        {
            if (user.AssignedSalesManId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);

            // Verify the salesman has a zone manager
            if (string.IsNullOrEmpty(approver.ZoneManagerId))
                return Result.Failure(ApprovalErrors.SalesManHasNoZoneManager);

            var fromStatus = user.RegistrationStatus;
            user.RegistrationStatus = RegistrationStatus.PendingZoneManager;

            await _userManager.UpdateAsync(user);
            await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Approved, fromStatus, user.RegistrationStatus);

            _logger.LogInformation(
                "SalesMan {ApproverId} approved user {UserId}. Status: PendingSalesman → PendingZoneManager",
                approverId, userId);

            return Result.Success();
        }

        // ZoneManager approval: PendingZoneManager → Approved
        if (user.RegistrationStatus == RegistrationStatus.PendingZoneManager
            && roles.Contains(UserRoles.ZoneManager))
        {
            // Verify this zone manager is the one the salesman reports to
            if (user.AssignedSalesMan?.ZoneManagerId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);

            var fromStatus = user.RegistrationStatus;
            user.RegistrationStatus = RegistrationStatus.Approved;

            // Generate unique ShopCode
            if (user.ShopOwnerProfile != null)
            {
                var shopCode = await GenerateShopCodeAsync();
                if (shopCode is null)
                    return Result.Failure(ApprovalErrors.ShopCodeGenerationFailed);

                user.ShopOwnerProfile.ShopCode = shopCode;
            }

            await _userManager.UpdateAsync(user);
            await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Approved, fromStatus, user.RegistrationStatus);

            _logger.LogInformation(
                "ZoneManager {ApproverId} approved user {UserId}. Status: PendingZoneManager → Approved. ShopCode: {ShopCode}",
                approverId, userId, user.ShopOwnerProfile?.ShopCode);

            // Send WhatsApp welcome (fire-and-forget, don't fail approval)
            _ = SendWelcomeMessageAsync(user);

            return Result.Success();
        }

        // User is not in a pending state that matches the approver's role
        if (user.RegistrationStatus != RegistrationStatus.PendingSalesman
            && user.RegistrationStatus != RegistrationStatus.PendingZoneManager)
        {
            return Result.Failure(ApprovalErrors.UserNotPendingApproval);
        }

        return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);
    }

    public async Task<Result> RejectAsync(string userId, string reason, string approverId)
    {
        var user = await _userManager.Users
            .Include(u => u.AssignedSalesMan)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var approver = await _userManager.FindByIdAsync(approverId);
        if (approver is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var roles = await _userManager.GetRolesAsync(approver);

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

        await _userManager.UpdateAsync(user);
        await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Rejected, fromStatus, RegistrationStatus.Rejected, reason);

        _logger.LogInformation(
            "Approver {ApproverId} rejected user {UserId}. Status: {FromStatus} → Rejected. Reason: {Reason}",
            approverId, userId, fromStatus, reason);

        return Result.Success();
    }

    #region Private Helpers

    private async Task<string?> GenerateShopCodeAsync(int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            var code = GenerateRandomCode(6);
            var exists = await _context.ShopOwnerProfiles.AnyAsync(p => p.ShopCode == code);
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
        string? rejectionReason = null)
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
        await _context.SaveChangesAsync();
    }

    private async Task SendWelcomeMessageAsync(ApplicationUser user)
    {
        try
        {
            var message = $"مرحباً {user.Name}! تمت الموافقة على تسجيلك في برنامج المكافآت. كود المحل الخاص بك: {user.ShopOwnerProfile?.ShopCode}";
            await _twilioRepository.SendWhatsAppMessageAsync(user.MobileNumber, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome WhatsApp message to user {UserId}", user.Id);
        }
    }

    #endregion
}
