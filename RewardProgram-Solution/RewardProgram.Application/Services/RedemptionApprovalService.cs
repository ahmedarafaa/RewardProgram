using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Redemption;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;
using RewardProgram.Application.Helpers;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services;

public class RedemptionApprovalService : IRedemptionApprovalService
{
    private const int MaxOtpAttempts = 5;

    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RedemptionApprovalService> _logger;

    public RedemptionApprovalService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<RedemptionApprovalService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<PaginatedResult<PendingRedemptionResponse>>> GetPendingAsync(
        string approverId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        (page, pageSize) = Helpers.PaginationHelper.Normalize(page, pageSize, maxPageSize: 50);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure<PaginatedResult<PendingRedemptionResponse>>(RedemptionErrors.NotAuthorizedToApprove);

        var roles = await _userRepository.GetRolesAsync(approver);

        var query = await BuildPendingScopedQueryAsync(approverId, roles, ct);
        if (query is null)
            return Result.Failure<PaginatedResult<PendingRedemptionResponse>>(RedemptionErrors.NotAuthorizedToApprove);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(r => r.User.Name.Contains(trimmed));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PendingRedemptionResponse(
                r.Id,
                r.User.Name,
                r.User.MobileNumber,
                r.Method,
                r.Status,
                r.PointsAmount,
                r.SarAmount,
                r.Iban,
                r.AccountNumber,
                r.Address,
                r.SwiftCode,
                r.AccountName,
                r.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<PendingRedemptionResponse>(items, totalCount, page, pageSize));
    }

    public async Task<Result<PaginatedResult<RedemptionListItem>>> GetListAsync(
        string approverId, RedemptionListStatusFilter status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Helpers.PaginationHelper.Normalize(page, pageSize, maxPageSize: 50);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure<PaginatedResult<RedemptionListItem>>(RedemptionErrors.NotAuthorizedToApprove);

        var roles = await _userRepository.GetRolesAsync(approver);
        if (!roles.Contains(UserRoles.SalesMan)
            && !roles.Contains(UserRoles.ZoneManager)
            && !roles.Contains(UserRoles.SystemAdmin))
            return Result.Failure<PaginatedResult<RedemptionListItem>>(RedemptionErrors.NotAuthorizedToApprove);

        var trimmed = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // Pending rows
        var includePending = status is RedemptionListStatusFilter.All or RedemptionListStatusFilter.Pending;
        var pendingRows = new List<RedemptionListRow>();
        var pendingCount = 0;
        if (includePending)
        {
            var pendingQ = await BuildPendingScopedQueryAsync(approverId, roles, ct);
            if (pendingQ is not null)
            {
                if (trimmed is not null)
                    pendingQ = pendingQ.Where(r => r.User.Name.Contains(trimmed));

                pendingCount = await pendingQ.CountAsync(ct);
                pendingRows = await pendingQ
                    .Select(r => new RedemptionListRow(
                        r.Id,
                        r.UserId,
                        r.User.Name,
                        r.User.MobileNumber,
                        r.Method,
                        r.PointsAmount,
                        r.SarAmount,
                        RedemptionReviewStatus.Pending,
                        r.Status,
                        r.CreatedAt,
                        null))
                    .ToListAsync(ct);
            }
        }

        // Reviewed rows (by this approver)
        var includeApproved = status is RedemptionListStatusFilter.All or RedemptionListStatusFilter.Approved;
        var includeRejected = status is RedemptionListStatusFilter.All or RedemptionListStatusFilter.Rejected;
        var reviewedRows = new List<RedemptionListRow>();
        var reviewedCount = 0;
        if (includeApproved || includeRejected)
        {
            var reviewedQ = _context.RedemptionApprovals.Where(a => a.ApproverId == approverId);
            if (includeApproved && !includeRejected)
                reviewedQ = reviewedQ.Where(a => a.Action == ApprovalAction.Approved);
            else if (includeRejected && !includeApproved)
                reviewedQ = reviewedQ.Where(a => a.Action == ApprovalAction.Rejected);

            if (trimmed is not null)
                reviewedQ = reviewedQ.Where(a => a.RedemptionRequest.User.Name.Contains(trimmed));

            reviewedCount = await reviewedQ.CountAsync(ct);
            reviewedRows = await reviewedQ
                .Select(a => new RedemptionListRow(
                    a.RedemptionRequestId,
                    a.RedemptionRequest.UserId,
                    a.RedemptionRequest.User.Name,
                    a.RedemptionRequest.User.MobileNumber,
                    a.RedemptionRequest.Method,
                    a.RedemptionRequest.PointsAmount,
                    a.RedemptionRequest.SarAmount,
                    a.Action == ApprovalAction.Approved ? RedemptionReviewStatus.Approved : RedemptionReviewStatus.Rejected,
                    a.RedemptionRequest.Status,
                    a.CreatedAt,
                    a.RejectionReason))
                .ToListAsync(ct);
        }

        var totalCount = pendingCount + reviewedCount;

        var pageRows = pendingRows
            .Concat(reviewedRows)
            .OrderByDescending(x => x.ActivityAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RedemptionListItem(
                r.RequestId,
                r.UserId,
                r.UserName,
                r.UserMobile,
                r.Method,
                r.PointsAmount,
                r.SarAmount,
                r.ReviewStatus,
                r.CurrentStatus,
                r.ActivityAt,
                r.RejectionReason))
            .ToList();

        return Result.Success(new PaginatedResult<RedemptionListItem>(pageRows, totalCount, page, pageSize));
    }

    private async Task<IQueryable<RedemptionRequest>?> BuildPendingScopedQueryAsync(
        string approverId, IList<string> roles, CancellationToken ct)
    {
        var query = _context.RedemptionRequests.AsQueryable();

        if (roles.Contains(UserRoles.SystemAdmin))
        {
            return query.Where(r => r.Status == RedemptionRequestStatus.PendingAdmin);
        }

        var hasSalesMan = roles.Contains(UserRoles.SalesMan);
        var hasZoneManager = roles.Contains(UserRoles.ZoneManager);

        if (!hasSalesMan && !hasZoneManager)
            return null;

        List<string> salesManCityIds = [];
        List<string> zoneManagerCityIds = [];

        if (hasSalesMan)
        {
            salesManCityIds = await _context.Cities
                .Where(c => c.ApprovalSalesManId == approverId)
                .Select(c => c.Id)
                .ToListAsync(ct);
        }

        if (hasZoneManager)
        {
            var managedRegion = await _context.Regions
                .FirstOrDefaultAsync(r => r.ZoneManagerId == approverId, ct);
            if (managedRegion is not null)
            {
                zoneManagerCityIds = await _context.Cities
                    .Where(c => c.RegionId == managedRegion.Id)
                    .Select(c => c.Id)
                    .ToListAsync(ct);
            }
        }

        // Dual-role users see BOTH queues: SM-pending from their SM cities
        // plus ZM-pending from their region's cities. The dual-role skip in
        // ApproveAsync still collapses SM+ZM into a single approval click.
        return query.Where(r =>
            r.User.NationalAddress != null &&
            ((r.Status == RedemptionRequestStatus.PendingSalesMan
                && salesManCityIds.Contains(r.User.NationalAddress.CityId))
             || (r.Status == RedemptionRequestStatus.PendingZoneManager
                && zoneManagerCityIds.Contains(r.User.NationalAddress.CityId))));
    }

    private sealed record RedemptionListRow(
        string RequestId,
        string UserId,
        string UserName,
        string UserMobile,
        RedemptionMethod Method,
        decimal PointsAmount,
        decimal SarAmount,
        RedemptionReviewStatus ReviewStatus,
        RedemptionRequestStatus CurrentStatus,
        DateTime ActivityAt,
        string? RejectionReason);

    public async Task<Result> ApproveAsync(
        ApproveRedemptionRequest request, string approverId, CancellationToken ct = default)
    {
        await using var transaction = await _context.BeginTransactionAsync(ct);

        var redemptionRequest = await _context.RedemptionRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == request.RedemptionRequestId, ct);

        if (redemptionRequest is null)
            return Result.Failure(RedemptionErrors.RequestNotFound);

        // Validate approver authorization for current status (inside transaction)
        var validationResult = await ValidateApproverAsync(redemptionRequest, approverId, ct);
        if (validationResult.IsFailure)
            return validationResult;

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        var approverRoles = approver is null ? [] : await _userRepository.GetRolesAsync(approver);

        // Dual-role skip fires only when the approver is also the ZoneManager
        // for this user's region — otherwise the real regional ZM must still review.
        var canSkipZoneManager = false;
        if (redemptionRequest.Status == RedemptionRequestStatus.PendingSalesMan
            && approverRoles.Contains(UserRoles.ZoneManager)
            && redemptionRequest.User?.NationalAddress is not null)
        {
            canSkipZoneManager = await _context.Cities
                .Where(c => c.Id == redemptionRequest.User.NationalAddress!.CityId)
                .AnyAsync(c => c.Region!.ZoneManagerId == approverId, ct);
        }

        var fromStatus = redemptionRequest.Status;
        var toStatus = GetNextApprovalStatus(fromStatus, canSkipZoneManager);

        redemptionRequest.Status = toStatus;

        // Record approval
        await _context.RedemptionApprovals.AddAsync(new RedemptionApproval
        {
            RedemptionRequestId = redemptionRequest.Id,
            ApproverId = approverId,
            Action = ApprovalAction.Approved,
            FromStatus = fromStatus,
            ToStatus = toStatus
        }, ct);

        // Cash OTP is issued up-front at request creation (see RedemptionService.CreateRequestAsync).
        // Admin approval only transitions state — the OTP the user already has remains valid.

        // If AdminApproved and BankTransfer → complete immediately
        if (toStatus == RedemptionRequestStatus.AdminApproved && redemptionRequest.Method == RedemptionMethod.BankTransfer)
        {
            redemptionRequest.Status = RedemptionRequestStatus.Completed;
            await CompleteRedemptionAsync(redemptionRequest, ct);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another approver/admin moved this request between our load and save.
            // RedemptionRequest.RowVersion (or Wallet.RowVersion in Complete) caught it.
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Concurrency conflict approving redemption {Id} by {ApproverId}",
                request.RedemptionRequestId, approverId);
            return Result.Failure(RedemptionErrors.ConcurrencyConflict);
        }

        _logger.LogInformation("Redemption request {Id} approved by {ApproverId}: {From} → {To}",
            redemptionRequest.Id, approverId, fromStatus, redemptionRequest.Status);

        if (redemptionRequest.Status == RedemptionRequestStatus.Completed)
        {
            await _notificationService.CreateAsync(redemptionRequest.UserId, NotificationType.RedemptionCompleted,
                title: "اكتمل طلب الاستبدال",
                body: "تم إتمام طلب الاستبدال بنجاح",
                referenceId: redemptionRequest.Id,
                titleKey: "Notif.Redemption.CompletedTitle",
                bodyKey: "Notif.Redemption.CompletedBody",
                ct: ct);
        }
        else
        {
            var (title, body, titleKey, bodyKey) = fromStatus switch
            {
                RedemptionRequestStatus.PendingSalesMan =>
                    ("تحديث طلب الاستبدال", "تمت موافقة مندوب المبيعات على طلب الاستبدال",
                     "Notif.Redemption.UpdateTitle", "Notif.Redemption.SalesManApprovedBody"),
                RedemptionRequestStatus.PendingZoneManager =>
                    ("تحديث طلب الاستبدال", "تمت موافقة مدير المنطقة على طلب الاستبدال",
                     "Notif.Redemption.UpdateTitle", "Notif.Redemption.ZoneManagerApprovedBody"),
                RedemptionRequestStatus.PendingAdmin =>
                    ("تمت موافقة الإدارة", "تمت موافقة الإدارة على طلب الاستبدال",
                     "Notif.Redemption.AdminApprovedTitle", "Notif.Redemption.AdminApprovedBody"),
                _ => ("تحديث طلب الاستبدال", "تمت الموافقة على طلب الاستبدال",
                      "Notif.Redemption.UpdateTitle", "Notif.Redemption.GenericApprovedBody")
            };

            await _notificationService.CreateAsync(redemptionRequest.UserId, NotificationType.RedemptionApproved,
                title: title, body: body, referenceId: redemptionRequest.Id,
                titleKey: titleKey, bodyKey: bodyKey,
                ct: ct);
        }

        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        RejectRedemptionRequest request, string approverId, CancellationToken ct = default)
    {
        await using var transaction = await _context.BeginTransactionAsync(ct);

        var redemptionRequest = await _context.RedemptionRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == request.RedemptionRequestId, ct);

        if (redemptionRequest is null)
            return Result.Failure(RedemptionErrors.RequestNotFound);

        var validationResult = await ValidateApproverAsync(redemptionRequest, approverId, ct);
        if (validationResult.IsFailure)
            return validationResult;

        var fromStatus = redemptionRequest.Status;

        redemptionRequest.Status = RedemptionRequestStatus.Rejected;
        redemptionRequest.RejectionReason = request.RejectionReason;
        redemptionRequest.RejectedById = approverId;

        await _context.RedemptionApprovals.AddAsync(new RedemptionApproval
        {
            RedemptionRequestId = redemptionRequest.Id,
            ApproverId = approverId,
            Action = ApprovalAction.Rejected,
            RejectionReason = request.RejectionReason,
            FromStatus = fromStatus,
            ToStatus = RedemptionRequestStatus.Rejected
        }, ct);

        // Refund held points
        await RefundPointsAsync(redemptionRequest, ct);

        try
        {
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Concurrency conflict rejecting redemption {Id} by {ApproverId}",
                request.RedemptionRequestId, approverId);
            return Result.Failure(RedemptionErrors.ConcurrencyConflict);
        }

        _logger.LogInformation("Redemption request {Id} rejected by {ApproverId}",
            redemptionRequest.Id, approverId);

        await _notificationService.CreateAsync(redemptionRequest.UserId, NotificationType.RedemptionRejected,
            title: "تم رفض طلب الاستبدال",
            body: $"تم رفض طلب الاستبدال. السبب: {request.RejectionReason}",
            referenceId: redemptionRequest.Id,
            titleKey: "Notif.Redemption.RejectedTitle",
            bodyKey: "Notif.Redemption.RejectedBody",
            bodyArgs: LocalizedTextRenderer.SerializeArgs(request.RejectionReason),
            ct: ct);

        return Result.Success();
    }

    public async Task<Result> ConfirmCashHandoverAsync(
        ConfirmCashHandoverRequest request, string handoverById, CancellationToken ct = default)
    {
        var redemptionRequest = await _context.RedemptionRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == request.RedemptionRequestId, ct);

        if (redemptionRequest is null)
            return Result.Failure(RedemptionErrors.RequestNotFound);

        if (redemptionRequest.Status != RedemptionRequestStatus.AdminApproved
            || redemptionRequest.Method != RedemptionMethod.Cash)
            return Result.Failure(RedemptionErrors.NotInCashHandoverState);

        // Scope check: only the SM for the user's city, the ZM for the user's region,
        // or a SystemAdmin may execute the cash handover.
        var handover = await _userRepository.FindByIdAsync(handoverById, ct);
        if (handover is null)
            return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);

        var handoverRoles = await _userRepository.GetRolesAsync(handover);
        if (!await IsApproverInScopeAsync(redemptionRequest.UserId, handoverById, handoverRoles, ct))
            return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);

        // Check OTP expiry
        if (redemptionRequest.CashOtpExpiresAt.HasValue && DateTime.UtcNow > redemptionRequest.CashOtpExpiresAt.Value)
        {
            // Auto-cancel and refund
            await using var cancelTx = await _context.BeginTransactionAsync(ct);
            redemptionRequest.Status = RedemptionRequestStatus.Cancelled;
            await RefundPointsAsync(redemptionRequest, ct);
            await _context.SaveChangesAsync(ct);
            await cancelTx.CommitAsync(ct);

            return Result.Failure(RedemptionErrors.OtpExpired);
        }

        // ATOMIC attempt-counter increment — single SQL statement that only succeeds
        // if attempts are still under the cap. Replaces the previous read-then-write
        // pattern where two concurrent attempts could both pass the cap check before
        // either persisted the increment.
        var incrementedRows = await _context.RedemptionRequests
            .Where(r => r.Id == redemptionRequest.Id
                && r.CashOtpAttempts < MaxOtpAttempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.CashOtpAttempts, r => r.CashOtpAttempts + 1),
                ct);

        // ExecuteUpdate above bumped RowVersion in the DB but bypassed the change
        // tracker. The tracked `redemptionRequest` still holds the pre-bump value, so
        // the next SaveChanges would issue UPDATE ... WHERE RowVersion=stale → fail.
        // ReloadAsync refreshes the tracker (and all properties) from the DB.
        await _context.Entry(redemptionRequest).ReloadAsync(ct);

        if (incrementedRows == 0)
        {
            // Already at the cap (either pre-existing or due to a concurrent attempt
            // that incremented while we were validating). Auto-cancel + refund.
            await using var lockoutTx = await _context.BeginTransactionAsync(ct);
            var current = await _context.RedemptionRequests
                .FirstOrDefaultAsync(r => r.Id == redemptionRequest.Id, ct);
            if (current is { Status: RedemptionRequestStatus.AdminApproved })
            {
                current.Status = RedemptionRequestStatus.Cancelled;
                await RefundPointsAsync(current, ct);
                try
                {
                    await _context.SaveChangesAsync(ct);
                    await lockoutTx.CommitAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await lockoutTx.RollbackAsync(ct);
                    // Another flow already cancelled — that's fine, still report lockout.
                }
            }

            return Result.Failure(RedemptionErrors.OtpMaxAttemptsExceeded);
        }

        // OTP hash hasn't changed since load; safe to compare against the in-memory value.
        if (redemptionRequest.CashOtpHash != HashOtp(request.Otp))
        {
            return Result.Failure(RedemptionErrors.InvalidOtp);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        // Reload inside the transaction so that the wallet RowVersion check below
        // sees the freshest state (the ExecuteUpdate above bypassed the tracker).
        var freshRequest = await _context.RedemptionRequests
            .FirstAsync(r => r.Id == redemptionRequest.Id, ct);

        if (freshRequest.Status != RedemptionRequestStatus.AdminApproved)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure(RedemptionErrors.NotInCashHandoverState);
        }

        freshRequest.Status = RedemptionRequestStatus.Completed;
        freshRequest.CashHandoverById = handoverById;
        freshRequest.CashHandoverAt = DateTime.UtcNow;

        await CompleteRedemptionAsync(freshRequest, ct);

        try
        {
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Wallet RowVersion or RedemptionRequest RowVersion caught a concurrent edit.
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Concurrency conflict completing cash handover {Id} by {HandoverBy}",
                freshRequest.Id, handoverById);
            return Result.Failure(RedemptionErrors.ConcurrencyConflict);
        }

        _logger.LogInformation("Cash handover confirmed for request {Id} by {HandoverBy}",
            freshRequest.Id, handoverById);

        await _notificationService.CreateAsync(freshRequest.UserId, NotificationType.RedemptionCompleted,
            title: "اكتمل طلب الاستبدال",
            body: "تم تسليم المبلغ بنجاح",
            referenceId: freshRequest.Id,
            titleKey: "Notif.Redemption.CompletedTitle",
            bodyKey: "Notif.Redemption.CashHandoverBody",
            ct: ct);

        return Result.Success();
    }

    // --- Private helpers ---

    private async Task<Result> ValidateApproverAsync(
        RedemptionRequest redemptionRequest, string approverId, CancellationToken ct)
    {
        if (redemptionRequest.Status is RedemptionRequestStatus.Completed
            or RedemptionRequestStatus.Rejected
            or RedemptionRequestStatus.Cancelled
            or RedemptionRequestStatus.AdminApproved)
            return Result.Failure(RedemptionErrors.NotPendingApproval);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);

        var roles = await _userRepository.GetRolesAsync(approver);

        // Role check
        var hasRole = redemptionRequest.Status switch
        {
            RedemptionRequestStatus.PendingSalesMan when roles.Contains(UserRoles.SalesMan) => true,
            RedemptionRequestStatus.PendingZoneManager when roles.Contains(UserRoles.ZoneManager) => true,
            RedemptionRequestStatus.PendingAdmin when roles.Contains(UserRoles.SystemAdmin) => true,
            _ => false
        };

        if (!hasRole)
            return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);

        // Geographic check — SalesMan must be assigned to user's city, ZoneManager to user's region
        if (redemptionRequest.Status == RedemptionRequestStatus.PendingSalesMan)
        {
            var user = redemptionRequest.User
                ?? await _userRepository.FindByIdAsync(redemptionRequest.UserId, ct);

            var userCityId = user?.NationalAddress?.CityId;
            if (userCityId is null)
                return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);

            var isAssigned = await _context.Cities
                .AnyAsync(c => c.Id == userCityId && c.ApprovalSalesManId == approverId, ct);

            if (!isAssigned)
                return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);
        }
        else if (redemptionRequest.Status == RedemptionRequestStatus.PendingZoneManager)
        {
            var user = redemptionRequest.User
                ?? await _userRepository.FindByIdAsync(redemptionRequest.UserId, ct);

            var userCityId = user?.NationalAddress?.CityId;
            if (userCityId is null)
                return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);

            var userRegionId = await _context.Cities
                .Where(c => c.Id == userCityId)
                .Select(c => c.RegionId)
                .FirstOrDefaultAsync(ct);

            var isAssigned = userRegionId is not null && await _context.Regions
                .AnyAsync(r => r.Id == userRegionId && r.ZoneManagerId == approverId, ct);

            if (!isAssigned)
                return Result.Failure(RedemptionErrors.NotAuthorizedToApprove);
        }

        return Result.Success();
    }

    private static RedemptionRequestStatus GetNextApprovalStatus(
        RedemptionRequestStatus current, bool canSkipZoneManager) => current switch
    {
        // Dual-role SM+ZM approver (for the user's region) collapses SM+ZM steps
        RedemptionRequestStatus.PendingSalesMan when canSkipZoneManager
            => RedemptionRequestStatus.PendingAdmin,
        RedemptionRequestStatus.PendingSalesMan => RedemptionRequestStatus.PendingZoneManager,
        RedemptionRequestStatus.PendingZoneManager => RedemptionRequestStatus.PendingAdmin,
        RedemptionRequestStatus.PendingAdmin => RedemptionRequestStatus.AdminApproved,
        _ => current
    };

    private async Task CompleteRedemptionAsync(RedemptionRequest redemptionRequest, CancellationToken ct)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == redemptionRequest.UserId, ct);
        if (wallet is null)
            throw new InvalidOperationException($"Wallet not found for user {redemptionRequest.UserId} during redemption completion");

        // FIFO: consume oldest points first
        var earnedTransactions = await _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id
                && t.Type == WalletTransactionType.Earned
                && t.RemainingAmount > 0)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var remaining = redemptionRequest.PointsAmount;
        foreach (var tx in earnedTransactions)
        {
            if (remaining <= 0) break;

            var consume = Math.Min(tx.RemainingAmount, remaining);
            tx.RemainingAmount -= consume;
            remaining -= consume;
        }

        if (remaining > 0)
        {
            _logger.LogError("FIFO_SHORTFALL: {Remaining} points could not be consumed from earned transactions for request {Id}. Wallet integrity compromised — aborting completion.",
                remaining, redemptionRequest.Id);
            throw new InvalidOperationException(
                $"Wallet integrity failure: {remaining} points could not be consumed via FIFO for redemption request {redemptionRequest.Id}.");
        }

        // Deduct from wallet
        wallet.Balance -= redemptionRequest.PointsAmount;
        wallet.SarBalance -= redemptionRequest.SarAmount;
        wallet.HeldBalance -= redemptionRequest.PointsAmount;
        wallet.HeldSarBalance -= redemptionRequest.SarAmount;

        // Record redeemed transaction
        var redeemKey = redemptionRequest.Method == RedemptionMethod.BankTransfer
            ? "Wallet.Tx.RedeemBank"
            : "Wallet.Tx.RedeemCash";
        await _context.WalletTransactions.AddAsync(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = -redemptionRequest.PointsAmount,
            Type = WalletTransactionType.Redeemed,
            ReferenceId = redemptionRequest.Id,
            Description = redemptionRequest.Method == RedemptionMethod.BankTransfer
                ? "استرداد — تحويل بنكي"
                : "استرداد — نقدي",
            DescriptionKey = redeemKey,
            SarRate = redemptionRequest.SarRate,
            SarAmount = -redemptionRequest.SarAmount
        }, ct);
    }

    private async Task RefundPointsAsync(RedemptionRequest redemptionRequest, CancellationToken ct)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == redemptionRequest.UserId, ct);
        if (wallet is null)
            throw new InvalidOperationException($"Wallet not found for user {redemptionRequest.UserId} during refund");

        wallet.HeldBalance -= redemptionRequest.PointsAmount;
        wallet.HeldSarBalance -= redemptionRequest.SarAmount;

        await _context.WalletTransactions.AddAsync(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = redemptionRequest.PointsAmount,
            Type = WalletTransactionType.Refunded,
            ReferenceId = redemptionRequest.Id,
            Description = "استرجاع نقاط — طلب مرفوض أو ملغي",
            DescriptionKey = "Wallet.Tx.Refund",
            SarRate = redemptionRequest.SarRate,
            SarAmount = redemptionRequest.SarAmount
        }, ct);
    }

    private async Task<bool> IsApproverInScopeAsync(
        string userId, string approverId, IList<string> approverRoles, CancellationToken ct)
    {
        if (approverRoles.Contains(UserRoles.SystemAdmin))
            return true;

        var user = await _userRepository.FindByIdAsync(userId, ct);
        var cityId = user?.NationalAddress?.CityId;
        if (cityId is null)
            return false;

        if (approverRoles.Contains(UserRoles.SalesMan))
        {
            var isSalesman = await _context.Cities
                .AnyAsync(c => c.Id == cityId && c.ApprovalSalesManId == approverId, ct);
            if (isSalesman) return true;
        }

        if (approverRoles.Contains(UserRoles.ZoneManager))
        {
            var regionId = await _context.Cities
                .Where(c => c.Id == cityId)
                .Select(c => c.RegionId)
                .FirstOrDefaultAsync(ct);
            if (regionId is not null)
            {
                var isZoneManager = await _context.Regions
                    .AnyAsync(r => r.Id == regionId && r.ZoneManagerId == approverId, ct);
                if (isZoneManager) return true;
            }
        }

        return false;
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexStringLower(bytes);
    }
}
