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
    private readonly ITwilioService _twilioService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RedemptionApprovalService> _logger;

    public RedemptionApprovalService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        ITwilioService twilioService,
        INotificationService notificationService,
        ILogger<RedemptionApprovalService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _twilioService = twilioService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<PaginatedResult<PendingRedemptionResponse>>> GetPendingAsync(
        string approverId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        (page, pageSize) = Helpers.PaginationHelper.Normalize(page, pageSize, maxPageSize: 50);

        var approver = await _userRepository.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Result.Failure<PaginatedResult<PendingRedemptionResponse>>(RedemptionErrors.NotAuthorizedToApprove);

        var roles = await _userRepository.GetRolesAsync(approver);

        var query = _context.RedemptionRequests
            .Include(r => r.User)
            .AsQueryable();

        // Filter by role-appropriate status and geographic assignment
        if (roles.Contains(UserRoles.SystemAdmin))
        {
            query = query.Where(r => r.Status == RedemptionRequestStatus.PendingAdmin);
        }
        else if (roles.Contains(UserRoles.ZoneManager))
        {
            // ZoneManager sees requests from users in their region
            var managedRegion = await _context.Regions
                .FirstOrDefaultAsync(r => r.ZoneManagerId == approverId, ct);

            if (managedRegion is null)
                return Result.Success(new PaginatedResult<PendingRedemptionResponse>([], 0, page, pageSize));

            var cityIds = await _context.Cities
                .Where(c => c.RegionId == managedRegion.Id)
                .Select(c => c.Id)
                .ToListAsync(ct);

            query = query.Where(r => r.Status == RedemptionRequestStatus.PendingZoneManager
                && r.User.NationalAddress != null
                && cityIds.Contains(r.User.NationalAddress.CityId));
        }
        else if (roles.Contains(UserRoles.SalesMan))
        {
            // SalesMan sees requests from users in their assigned cities
            var assignedCityIds = await _context.Cities
                .Where(c => c.ApprovalSalesManId == approverId)
                .Select(c => c.Id)
                .ToListAsync(ct);

            query = query.Where(r => r.Status == RedemptionRequestStatus.PendingSalesMan
                && r.User.NationalAddress != null
                && assignedCityIds.Contains(r.User.NationalAddress.CityId));
        }
        else
        {
            return Result.Failure<PaginatedResult<PendingRedemptionResponse>>(RedemptionErrors.NotAuthorizedToApprove);
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

        var fromStatus = redemptionRequest.Status;
        var toStatus = GetNextApprovalStatus(fromStatus);

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

        // If AdminApproved and Cash method → generate OTP
        if (toStatus == RedemptionRequestStatus.AdminApproved && redemptionRequest.Method == RedemptionMethod.Cash)
        {
            var otp = GenerateOtp();
            redemptionRequest.CashOtpHash = HashOtp(otp);
            redemptionRequest.CashOtpExpiresAt = DateTime.UtcNow.AddDays(14);
            redemptionRequest.CashOtpAttempts = 0;

            // Send OTP via WhatsApp
            var mobile = redemptionRequest.User.MobileNumber;
            if (!string.IsNullOrEmpty(mobile))
            {
                const string cashOtpTemplateSid = "HX345d6229ba79de58919c1daab1d1dfbc";
                var variables = new Dictionary<string, string> { { "1", otp } };
                await _twilioService.SendWhatsAppMessageAsync(mobile, cashOtpTemplateSid, variables, ct);
            }
        }

        // If AdminApproved and BankTransfer → complete immediately
        if (toStatus == RedemptionRequestStatus.AdminApproved && redemptionRequest.Method == RedemptionMethod.BankTransfer)
        {
            redemptionRequest.Status = RedemptionRequestStatus.Completed;
            await CompleteRedemptionAsync(redemptionRequest, ct);
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Redemption request {Id} approved by {ApproverId}: {From} → {To}",
            redemptionRequest.Id, approverId, fromStatus, redemptionRequest.Status);

        if (redemptionRequest.Status == RedemptionRequestStatus.Completed)
        {
            await _notificationService.CreateAsync(redemptionRequest.UserId, NotificationType.RedemptionCompleted,
                "اكتمل طلب الاستبدال", "تم إتمام طلب الاستبدال بنجاح",
                redemptionRequest.Id, ct);
        }
        else
        {
            await _notificationService.CreateAsync(redemptionRequest.UserId, NotificationType.RedemptionApproved,
                "تحديث طلب الاستبدال", "تمت الموافقة على طلب الاستبدال وانتقل للمرحلة التالية",
                redemptionRequest.Id, ct);
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

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Redemption request {Id} rejected by {ApproverId}",
            redemptionRequest.Id, approverId);

        await _notificationService.CreateAsync(redemptionRequest.UserId, NotificationType.RedemptionRejected,
            "تم رفض طلب الاستبدال", $"تم رفض طلب الاستبدال. السبب: {request.RejectionReason}",
            redemptionRequest.Id, ct);

        return Result.Success();
    }

    public async Task<Result> ConfirmCashHandoverAsync(
        ConfirmCashHandoverRequest request, string handoverById, CancellationToken ct = default)
    {
        var redemptionRequest = await _context.RedemptionRequests
            .FirstOrDefaultAsync(r => r.Id == request.RedemptionRequestId, ct);

        if (redemptionRequest is null)
            return Result.Failure(RedemptionErrors.RequestNotFound);

        if (redemptionRequest.Status != RedemptionRequestStatus.AdminApproved
            || redemptionRequest.Method != RedemptionMethod.Cash)
            return Result.Failure(RedemptionErrors.NotInCashHandoverState);

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

        // Check brute-force limit — auto-cancel and refund on lockout
        if (redemptionRequest.CashOtpAttempts >= MaxOtpAttempts)
        {
            await using var lockoutTx = await _context.BeginTransactionAsync(ct);
            redemptionRequest.Status = RedemptionRequestStatus.Cancelled;
            await RefundPointsAsync(redemptionRequest, ct);
            await _context.SaveChangesAsync(ct);
            await lockoutTx.CommitAsync(ct);

            return Result.Failure(RedemptionErrors.OtpMaxAttemptsExceeded);
        }

        // Validate OTP (hash comparison)
        if (redemptionRequest.CashOtpHash != HashOtp(request.Otp))
        {
            redemptionRequest.CashOtpAttempts++;
            await _context.SaveChangesAsync(ct);
            return Result.Failure(RedemptionErrors.InvalidOtp);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);

        redemptionRequest.Status = RedemptionRequestStatus.Completed;
        redemptionRequest.CashHandoverById = handoverById;
        redemptionRequest.CashHandoverAt = DateTime.UtcNow;

        await CompleteRedemptionAsync(redemptionRequest, ct);

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Cash handover confirmed for request {Id} by {HandoverBy}",
            redemptionRequest.Id, handoverById);

        await _notificationService.CreateAsync(redemptionRequest.UserId, NotificationType.RedemptionCompleted,
            "اكتمل طلب الاستبدال", "تم تسليم المبلغ بنجاح",
            redemptionRequest.Id, ct);

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

    private static RedemptionRequestStatus GetNextApprovalStatus(RedemptionRequestStatus current) => current switch
    {
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
            _logger.LogWarning("FIFO shortfall: {Remaining} points could not be consumed from earned transactions for request {Id}",
                remaining, redemptionRequest.Id);

        // Deduct from wallet
        wallet.Balance -= redemptionRequest.PointsAmount;
        wallet.SarBalance -= redemptionRequest.SarAmount;
        wallet.HeldBalance -= redemptionRequest.PointsAmount;
        wallet.HeldSarBalance -= redemptionRequest.SarAmount;

        // Record redeemed transaction
        await _context.WalletTransactions.AddAsync(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = -redemptionRequest.PointsAmount,
            Type = WalletTransactionType.Redeemed,
            ReferenceId = redemptionRequest.Id,
            Description = redemptionRequest.Method == RedemptionMethod.BankTransfer
                ? "استرداد — تحويل بنكي"
                : "استرداد — نقدي",
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
            SarRate = redemptionRequest.SarRate,
            SarAmount = redemptionRequest.SarAmount
        }, ct);
    }

    private static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexStringLower(bytes);
    }
}
