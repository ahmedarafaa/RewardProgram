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
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure<PaginatedResult<PendingUserResponse>>(AuthErrors.UserNotFound);

        var roles = await _userRepository.GetRolesAsync(approver);

        var isSalesMan = roles.Contains(UserRoles.SalesMan);
        var isZoneManager = roles.Contains(UserRoles.ZoneManager);

        if (!isSalesMan && !isZoneManager)
            return Result.Failure<PaginatedResult<PendingUserResponse>>(ApprovalErrors.NotAuthorizedToApprove);

        IQueryable<ApplicationUser> query;

        if (isSalesMan && isZoneManager)
        {
            // Dual-role: show both SalesMan pending + ZoneManager pending
            var managedCityIds = _context.Cities
                .Where(c => c.Region!.ZoneManagerId == approverId)
                .Select(c => c.Id);

            query = _userRepository.Query()
                .Where(u =>
                    (u.AssignedSalesManId == approverId && u.RegistrationStatus == RegistrationStatus.PendingSalesman)
                    ||
                    (u.RegistrationStatus == RegistrationStatus.PendingZoneManager
                     && u.NationalAddress != null
                     && managedCityIds.Contains(u.NationalAddress.CityId)));
        }
        else if (isSalesMan)
        {
            query = _userRepository.Query()
                .Where(u => u.AssignedSalesManId == approverId
                         && u.RegistrationStatus == RegistrationStatus.PendingSalesman);
        }
        else
        {
            // Pure ZoneManager
            var managedCityIds = _context.Cities
                .Where(c => c.Region!.ZoneManagerId == approverId)
                .Select(c => c.Id);

            query = _userRepository.Query()
                .Where(u => u.RegistrationStatus == RegistrationStatus.PendingZoneManager
                         && u.NationalAddress != null
                         && managedCityIds.Contains(u.NationalAddress.CityId));
        }

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.ShopOwnerProfile)
            .Include(u => u.SellerProfile)
            .Include(u => u.AssignedSalesMan)
            .ToListAsync(ct);

        // Resolve city/region names in bulk
        var cityIds = users
            .Where(u => u.NationalAddress != null)
            .Select(u => u.NationalAddress!.CityId)
            .Distinct()
            .ToList();

        var cities = await _context.Cities
            .Where(c => cityIds.Contains(c.Id))
            .Include(c => c.Region)
            .ToDictionaryAsync(c => c.Id, c => new { c.NameAr, RegionName = c.Region.NameAr }, ct);

        // Collect CustomerCodes from profiles
        var customerCodes = users
            .Select(u => u.ShopOwnerProfile?.CustomerCode ?? u.SellerProfile?.CustomerCode)
            .Where(cc => cc != null)
            .Distinct()
            .ToList();

        // Bulk lookup ShopData by CustomerCode
        var shopDataMap = customerCodes.Count > 0
            ? await _context.ShopData
                .Where(sd => customerCodes.Contains(sd.CustomerCode))
                .ToDictionaryAsync(sd => sd.CustomerCode, sd => sd, ct)
            : new Dictionary<string, ShopData>();

        // Bulk lookup ErpCustomer by CustomerCode
        var erpCustomerMap = customerCodes.Count > 0
            ? await _context.ErpCustomers
                .Where(e => customerCodes.Contains(e.CustomerCode))
                .ToDictionaryAsync(e => e.CustomerCode, e => e.CustomerName, ct)
            : new Dictionary<string, string>();

        var items = users.Select(u =>
        {
            string? cityName = null;
            string? regionName = null;

            if (u.NationalAddress != null)
            {
                if (cities.TryGetValue(u.NationalAddress.CityId, out var cityInfo))
                {
                    cityName = cityInfo.NameAr;
                    regionName = cityInfo.RegionName;
                }
            }

            var customerCode = u.ShopOwnerProfile?.CustomerCode ?? u.SellerProfile?.CustomerCode;
            string? customerName = null;
            string? storeName = null;
            string? vat = null;
            string? crn = null;
            string? shopImageUrl = null;

            if (customerCode != null)
            {
                erpCustomerMap.TryGetValue(customerCode, out customerName);

                if (shopDataMap.TryGetValue(customerCode, out var sd))
                {
                    storeName = sd.StoreName;
                    vat = sd.VAT;
                    crn = sd.CRN;
                    shopImageUrl = sd.ShopImageUrl;
                }
            }

            return new PendingUserResponse(
                Id: u.Id,
                Name: u.Name,
                MobileNumber: u.MobileNumber,
                UserType: u.UserType,
                RegistrationStatus: u.RegistrationStatus,
                RegisteredAt: u.CreatedAt,
                CustomerCode: customerCode,
                CustomerName: customerName,
                StoreName: storeName,
                VAT: vat,
                CRN: crn,
                ShopImageUrl: shopImageUrl,
                RegionName: regionName,
                CityName: cityName,
                Street: u.NationalAddress?.Street,
                BuildingNumber: u.NationalAddress?.BuildingNumber,
                PostalCode: u.NationalAddress?.PostalCode,
                SubNumber: u.NationalAddress?.SubNumber,
                AssignedSalesManName: u.AssignedSalesMan?.Name
            );
        }).ToList();

        var result = new PaginatedResult<PendingUserResponse>(items, totalCount, page, pageSize);
        return Result.Success(result);
    }

    public async Task<Result> ApproveAsync(string userId, string approverId, CancellationToken ct = default)
    {
        var user = await _userRepository.Query()
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

            // Verify region has a ZoneManager (derived from user's city → region)
            var regionHasZoneManager = user.NationalAddress != null
                && await _context.Cities
                    .Where(c => c.Id == user.NationalAddress.CityId)
                    .Select(c => c.Region!.ZoneManagerId)
                    .FirstOrDefaultAsync(ct) != null;

            if (!regionHasZoneManager)
                return Result.Failure(ApprovalErrors.NoZoneManagerForRegion);

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
            // Verify this ZoneManager manages the region of the user's city
            var isAuthorized = user.NationalAddress != null
                && await _context.Cities
                    .Where(c => c.Id == user.NationalAddress.CityId)
                    .AnyAsync(c => c.Region!.ZoneManagerId == approverId, ct);

            if (!isAuthorized)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);

            var fromStatus = user.RegistrationStatus;
            user.RegistrationStatus = RegistrationStatus.Approved;

            await _userRepository.UpdateAsync(user);
            await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Approved, fromStatus, user.RegistrationStatus, ct: ct);

            _logger.LogInformation(
                "ZoneManager {ApproverId} approved user {UserId}. Status: PendingZoneManager → Approved",
                approverId, userId);

            // Send WhatsApp welcome (fire-and-forget with captured data)
            var welcomeMobile = user.MobileNumber;
            var welcomeName = user.Name;
            _ = SendWelcomeMessageAsync(welcomeMobile, welcomeName);

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
            // Verify this ZoneManager manages the region of the user's city
            var isAuthorized = user.NationalAddress != null
                && await _context.Cities
                    .Where(c => c.Id == user.NationalAddress.CityId)
                    .AnyAsync(c => c.Region!.ZoneManagerId == approverId, ct);

            if (!isAuthorized)
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

    private async Task SendWelcomeMessageAsync(string mobileNumber, string userName)
    {
        try
        {
            var message = $"مرحباً {userName}! تمت الموافقة على طلبك، يمكنك الآن تسجيل الدخول";
            await _twilioService.SendWhatsAppMessageAsync(mobileNumber, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome WhatsApp message to {Mobile}", MobileNumberHelper.Mask(mobileNumber));
        }
    }

    #endregion
}
