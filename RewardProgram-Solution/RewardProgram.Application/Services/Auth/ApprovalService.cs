using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using static RewardProgram.Application.Helpers.PaginationHelper;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Auth;

public class ApprovalService : IApprovalService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ITwilioService _twilioService;
    private readonly IInvitationService _invitationService;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        ITwilioService twilioService,
        IInvitationService invitationService,
        ILogger<ApprovalService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _twilioService = twilioService;
        _invitationService = invitationService;
        _logger = logger;
    }

    public async Task<Result<PaginatedResult<PendingUserResponse>>> GetPendingRequestsAsync(string approverId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize, maxPageSize: 50);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure<PaginatedResult<PendingUserResponse>>(AuthErrors.UserNotFound);

        var roles = await _userRepository.GetRolesAsync(approver);

        var isSalesMan = roles.Contains(UserRoles.SalesMan);
        var isZoneManager = roles.Contains(UserRoles.ZoneManager);

        if (!isSalesMan && !isZoneManager)
            return Result.Failure<PaginatedResult<PendingUserResponse>>(ApprovalErrors.NotAuthorizedToApprove);

        var query = BuildPendingScopedQuery(approverId, isSalesMan, isZoneManager);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(u => u.Name.Contains(trimmed));
        }

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.ShopOwnerProfile)
            .Include(u => u.SellerProfile)
            .Include(u => u.AssignedSalesMan)
            .Include(u => u.InvitedByUser)
            .ToListAsync(ct);

        // Resolve city/region names in bulk
        var cityIds = users
            .Where(u => u.NationalAddress != null)
            .Select(u => u.NationalAddress!.CityId)
            .Distinct()
            .ToList();

        // Pick city/region names per the active request culture so the approval
        // queue surfaces English in English mode and Arabic in Arabic mode.
        var isEnglish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("en", StringComparison.OrdinalIgnoreCase);

        var cities = await _context.Cities
            .Where(c => cityIds.Contains(c.Id))
            .Include(c => c.Region)
            .ToDictionaryAsync(
                c => c.Id,
                c => new
                {
                    CityName = isEnglish ? c.NameEn : c.NameAr,
                    RegionName = isEnglish ? c.Region.NameEn : c.Region.NameAr
                },
                ct);

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
                    cityName = cityInfo.CityName;
                    regionName = cityInfo.RegionName;
                }
            }

            var customerCode = u.ShopOwnerProfile?.CustomerCode ?? u.SellerProfile?.CustomerCode;
            string? customerName = null;
            string? storeName = null;
            string? vat = null;
            string? crn = null;
            string? shortAddress = null;
            string? shopImageUrl = null;

            if (customerCode != null)
            {
                erpCustomerMap.TryGetValue(customerCode, out customerName);

                if (shopDataMap.TryGetValue(customerCode, out var sd))
                {
                    storeName = sd.StoreName;
                    vat = sd.VAT;
                    crn = sd.CRN;
                    shortAddress = sd.ShortAddress;
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
                ShortAddress: shortAddress,
                ShopImageUrl: shopImageUrl,
                RegionName: regionName,
                CityName: cityName,
                Street: u.NationalAddress?.Street,
                BuildingNumber: u.NationalAddress?.BuildingNumber,
                PostalCode: u.NationalAddress?.PostalCode,
                SubNumber: u.NationalAddress?.SubNumber,
                District: u.NationalAddress?.District,
                AssignedSalesManName: u.AssignedSalesMan?.Name,
                InvitedByName: u.InvitedByUser?.Name
            );
        }).ToList();

        var result = new PaginatedResult<PendingUserResponse>(items, totalCount, page, pageSize);
        return Result.Success(result);
    }

    public async Task<Result<PaginatedResult<ApprovalListItem>>> GetListAsync(
        string approverId, ApprovalListStatusFilter status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize, maxPageSize: 50);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure<PaginatedResult<ApprovalListItem>>(AuthErrors.UserNotFound);

        var roles = await _userRepository.GetRolesAsync(approver);

        var isSalesMan = roles.Contains(UserRoles.SalesMan);
        var isZoneManager = roles.Contains(UserRoles.ZoneManager);

        if (!isSalesMan && !isZoneManager)
            return Result.Failure<PaginatedResult<ApprovalListItem>>(ApprovalErrors.NotAuthorizedToApprove);

        var trimmed = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // Pending rows (current pending-for-this-approver users)
        var includePending = status is ApprovalListStatusFilter.All or ApprovalListStatusFilter.Pending;
        var pendingRows = new List<ApprovalListRow>();
        var pendingCount = 0;
        if (includePending)
        {
            var pendingQ = BuildPendingScopedQuery(approverId, isSalesMan, isZoneManager);
            if (trimmed is not null)
                pendingQ = pendingQ.Where(u => u.Name.Contains(trimmed));

            pendingCount = await pendingQ.CountAsync(ct);
            pendingRows = await pendingQ
                .Select(u => new ApprovalListRow(
                    u.Id,
                    u.Name,
                    u.MobileNumber,
                    u.UserType,
                    u.ShopOwnerProfile != null
                        ? u.ShopOwnerProfile.CustomerCode
                        : (u.SellerProfile != null ? u.SellerProfile.CustomerCode : null),
                    ApprovalReviewStatus.Pending,
                    u.RegistrationStatus,
                    u.CreatedAt,
                    null))
                .ToListAsync(ct);
        }

        // Reviewed rows (records this approver decided on)
        var includeApproved = status is ApprovalListStatusFilter.All or ApprovalListStatusFilter.Approved;
        var includeRejected = status is ApprovalListStatusFilter.All or ApprovalListStatusFilter.Rejected;
        var reviewedRows = new List<ApprovalListRow>();
        var reviewedCount = 0;
        if (includeApproved || includeRejected)
        {
            var reviewedQ = _context.ApprovalRecords.Where(r => r.ApproverId == approverId);
            if (includeApproved && !includeRejected)
                reviewedQ = reviewedQ.Where(r => r.Action == ApprovalAction.Approved);
            else if (includeRejected && !includeApproved)
                reviewedQ = reviewedQ.Where(r => r.Action == ApprovalAction.Rejected);

            if (trimmed is not null)
                reviewedQ = reviewedQ.Where(r => r.User.Name.Contains(trimmed));

            reviewedCount = await reviewedQ.CountAsync(ct);
            reviewedRows = await reviewedQ
                .Select(r => new ApprovalListRow(
                    r.UserId,
                    r.User.Name,
                    r.User.MobileNumber,
                    r.User.UserType,
                    r.User.ShopOwnerProfile != null
                        ? r.User.ShopOwnerProfile.CustomerCode
                        : (r.User.SellerProfile != null ? r.User.SellerProfile.CustomerCode : null),
                    r.Action == ApprovalAction.Approved ? ApprovalReviewStatus.Approved : ApprovalReviewStatus.Rejected,
                    r.User.RegistrationStatus,
                    r.CreatedAt,
                    r.RejectionReason))
                .ToListAsync(ct);
        }

        var totalCount = pendingCount + reviewedCount;

        var pageRows = pendingRows
            .Concat(reviewedRows)
            .OrderByDescending(x => x.ActivityAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Resolve StoreName for the page in one batch
        var customerCodes = pageRows
            .Where(r => r.CustomerCode != null)
            .Select(r => r.CustomerCode!)
            .Distinct()
            .ToList();

        var storeNameMap = customerCodes.Count > 0
            ? await _context.ShopData
                .Where(sd => customerCodes.Contains(sd.CustomerCode))
                .ToDictionaryAsync(sd => sd.CustomerCode, sd => sd.StoreName, ct)
            : new Dictionary<string, string>();

        var items = pageRows.Select(r => new ApprovalListItem(
            r.UserId,
            r.Name,
            r.MobileNumber,
            r.UserType,
            r.CustomerCode != null && storeNameMap.TryGetValue(r.CustomerCode, out var sn) ? sn : null,
            r.ReviewStatus,
            r.CurrentStatus,
            r.ActivityAt,
            r.RejectionReason)).ToList();

        return Result.Success(new PaginatedResult<ApprovalListItem>(items, totalCount, page, pageSize));
    }

    private IQueryable<ApplicationUser> BuildPendingScopedQuery(string approverId, bool isSalesMan, bool isZoneManager)
    {
        if (isSalesMan && isZoneManager)
        {
            var managedCityIds = _context.Cities
                .Where(c => c.Region!.ZoneManagerId == approverId)
                .Select(c => c.Id);

            return _userRepository.Query()
                .Where(u =>
                    (u.AssignedSalesManId == approverId && u.RegistrationStatus == RegistrationStatus.PendingSalesman)
                    ||
                    (u.RegistrationStatus == RegistrationStatus.PendingZoneManager
                     && u.NationalAddress != null
                     && managedCityIds.Contains(u.NationalAddress.CityId)));
        }

        if (isSalesMan)
        {
            return _userRepository.Query()
                .Where(u => u.AssignedSalesManId == approverId
                         && u.RegistrationStatus == RegistrationStatus.PendingSalesman);
        }

        var zmCityIds = _context.Cities
            .Where(c => c.Region!.ZoneManagerId == approverId)
            .Select(c => c.Id);

        return _userRepository.Query()
            .Where(u => u.RegistrationStatus == RegistrationStatus.PendingZoneManager
                     && u.NationalAddress != null
                     && zmCityIds.Contains(u.NationalAddress.CityId));
    }

    private sealed record ApprovalListRow(
        string UserId,
        string Name,
        string MobileNumber,
        UserType UserType,
        string? CustomerCode,
        ApprovalReviewStatus ReviewStatus,
        RegistrationStatus CurrentStatus,
        DateTime ActivityAt,
        string? RejectionReason);

    public async Task<Result> ApproveAsync(string userId, string approverId, CancellationToken ct = default)
    {
        await using var transaction = await _context.BeginTransactionAsync(ct);

        var user = await _userRepository.Query()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var roles = await _userRepository.GetRolesAsync(approver);

        // SalesMan approval: PendingSalesman → PendingZoneManager
        // (or → Approved if approver also manages user's region as ZoneManager)
        if (user.RegistrationStatus == RegistrationStatus.PendingSalesman
            && roles.Contains(UserRoles.SalesMan))
        {
            // Invariant: registration requires a NationalAddress and the user's city
            // always has an SM assigned (enforced by SM reassign / delete-with-reassign).
            // If we land here without one, treat it as data corruption, not a 4xx.
            if (user.NationalAddress is null)
                throw new InvalidOperationException(
                    $"User {userId} reached SM approval with no NationalAddress — invariant violated.");

            if (user.AssignedSalesManId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);

            // Dual-role skip: if approver also manages user's region as ZM,
            // collapse SM+ZM steps into one and jump straight to Approved.
            var isZmForUserRegion = roles.Contains(UserRoles.ZoneManager)
                && await _context.Cities
                    .Where(c => c.Id == user.NationalAddress.CityId)
                    .AnyAsync(c => c.Region!.ZoneManagerId == approverId, ct);

            if (!isZmForUserRegion)
            {
                // Standard SM-only path — still need a ZoneManager downstream.
                var regionHasZoneManager = await _context.Cities
                    .Where(c => c.Id == user.NationalAddress.CityId)
                    .Select(c => c.Region!.ZoneManagerId)
                    .FirstOrDefaultAsync(ct) != null;

                if (!regionHasZoneManager)
                    return Result.Failure(ApprovalErrors.NoZoneManagerForRegion);
            }

            var fromStatus = user.RegistrationStatus;
            user.RegistrationStatus = isZmForUserRegion
                ? RegistrationStatus.Approved
                : RegistrationStatus.PendingZoneManager;

            await _userRepository.UpdateAsync(user);
            await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Approved, fromStatus, user.RegistrationStatus, ct: ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Approver {ApproverId} approved user {UserId} at SalesMan step. Status: {From} → {To}",
                approverId, userId, fromStatus, user.RegistrationStatus);

            if (isZmForUserRegion)
                await ApplyApprovedSideEffectsAsync(user, ct);

            return Result.Success();
        }

        // ZoneManager approval: PendingZoneManager → Approved
        if (user.RegistrationStatus == RegistrationStatus.PendingZoneManager
            && roles.Contains(UserRoles.ZoneManager))
        {
            if (user.NationalAddress is null)
                throw new InvalidOperationException(
                    $"User {userId} reached ZM approval with no NationalAddress — invariant violated.");

            // Re-read the region's current ZoneManagerId — it can be null (region
            // was never assigned, or admin cleared it after registration), in which
            // case we surface NoZoneManagerForRegion instead of a misleading 403.
            var regionZoneManagerId = await _context.Cities
                .Where(c => c.Id == user.NationalAddress.CityId)
                .Select(c => c.Region!.ZoneManagerId)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrEmpty(regionZoneManagerId))
                return Result.Failure(ApprovalErrors.NoZoneManagerForRegion);

            if (regionZoneManagerId != approverId)
                return Result.Failure(ApprovalErrors.NotAuthorizedToApprove);

            var fromStatus = user.RegistrationStatus;
            user.RegistrationStatus = RegistrationStatus.Approved;

            await _userRepository.UpdateAsync(user);
            await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Approved, fromStatus, user.RegistrationStatus, ct: ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "ZoneManager {ApproverId} approved user {UserId}. Status: PendingZoneManager → Approved",
                approverId, userId);

            await ApplyApprovedSideEffectsAsync(user, ct);

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
        await using var transaction = await _context.BeginTransactionAsync(ct);

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

        // Revoke all active refresh tokens — prevents rejected users from continuing
        // to mint access tokens via the refresh endpoint for up to 365 days.
        foreach (var token in user.RefreshTokens.Where(t => t.RevokedOn == null))
            token.RevokedOn = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        await LogApprovalRecordAsync(userId, approverId, ApprovalAction.Rejected, fromStatus, RegistrationStatus.Rejected, reason, ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Approver {ApproverId} rejected user {UserId}. Status: {FromStatus} → Rejected. Reason: {Reason}",
            approverId, userId, fromStatus, reason);

        // Rejection is delivered via WhatsApp (no FCM — rejected users can't log in).
        var rejectMobile = user.MobileNumber;
        var rejectName = user.Name;
        if (!string.IsNullOrEmpty(rejectMobile))
            _ = SendRejectionMessageAsync(rejectMobile, rejectName, reason);

        return Result.Success();
    }

    #region Private Helpers

    private async Task ApplyApprovedSideEffectsAsync(ApplicationUser user, CancellationToken ct)
    {
        // Credit invitation rewards in a separate transaction (after approval is committed).
        // Approval must not be rolled back if reward crediting fails; reward service manages its own transaction.
        if (user.InvitedByUserId is not null)
        {
            try
            {
                await _invitationService.CreditInvitationRewardsAsync(user.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "REWARD_CREDITING_FAILED: Invitation rewards not credited for approved user {UserId}. Manual intervention may be needed.", user.Id);
            }
        }

        // WhatsApp welcome is the user-facing notification on approval (no FCM in-app notification).
        var welcomeMobile = user.MobileNumber;
        var welcomeName = user.Name;
        _ = SendWelcomeMessageAsync(welcomeMobile, welcomeName);
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

    private async Task SendWelcomeMessageAsync(string mobileNumber, string userName)
    {
        try
        {
            const string welcomeTemplateSid = "HX34492ec3a59b30d95256da95a5515723";
            var variables = new Dictionary<string, string> { { "1", userName } };
            await _twilioService.SendWhatsAppMessageAsync(mobileNumber, welcomeTemplateSid, variables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome WhatsApp to {Mobile}", MobileNumberHelper.Mask(mobileNumber));
        }
    }

    private async Task SendRejectionMessageAsync(string mobileNumber, string userName, string reason)
    {
        try
        {
            const string rejectionTemplateSid = "HX7f13ecc96c50972b7bd99095ba691edb";
            var variables = new Dictionary<string, string>
            {
                { "1", userName },
                { "2", reason }
            };
            await _twilioService.SendWhatsAppMessageAsync(mobileNumber, rejectionTemplateSid, variables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send rejection WhatsApp to {Mobile}", MobileNumberHelper.Mask(mobileNumber));
        }
    }

    #endregion
}
