using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Redemption;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services;

public class RedemptionService : IRedemptionService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RedemptionService> _logger;

    public RedemptionService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<RedemptionService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<RedemptionRequestResponse>> CreateRequestAsync(
        CreateRedemptionRequest request, string userId, CancellationToken ct = default)
    {
        // 1. Validate user
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null || user.IsDisabled || user.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<RedemptionRequestResponse>(RedemptionErrors.UserNotApproved);

        // 2. Get settings (SAR rate + minimum points)
        var settings = await _context.RewardSettings.FirstOrDefaultAsync(ct);
        var minimumPoints = settings?.MinimumRedemptionPoints ?? 1000m;

        // 3. Check minimum points (dynamic from admin settings)
        if (request.PointsAmount < minimumPoints)
            return Result.Failure<RedemptionRequestResponse>(RedemptionErrors.BelowMinimum);
        var sarRate = settings?.PointsToSarRate ?? 10m;
        var sarAmount = request.PointsAmount / sarRate;

        // 4. Check SAR is integer
        if (sarAmount != Math.Floor(sarAmount))
            return Result.Failure<RedemptionRequestResponse>(RedemptionErrors.NotIntegerSar);

        // 5. Determine first approval level
        var city = await GetUserCityAsync(user);
        var status = city?.ApprovalSalesManId is not null
            ? RedemptionRequestStatus.PendingSalesMan
            : RedemptionRequestStatus.PendingZoneManager;

        // 6. Begin transaction — all checks that guard against concurrent requests must be inside
        await using var transaction = await _context.BeginTransactionAsync(ct);

        // 7. Check no pending request (inside transaction to prevent race condition)
        var hasPending = await _context.RedemptionRequests
            .AnyAsync(r => r.UserId == userId && r.Status != RedemptionRequestStatus.Completed
                && r.Status != RedemptionRequestStatus.Rejected
                && r.Status != RedemptionRequestStatus.Cancelled, ct);

        if (hasPending)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure<RedemptionRequestResponse>(RedemptionErrors.AlreadyHasPendingRequest);
        }

        // 8. Check available balance (inside transaction to prevent double-hold)
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet is null)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure<RedemptionRequestResponse>(RedemptionErrors.InsufficientBalance);
        }

        await ExpireOldPointsAsync(wallet, ct);

        var availableBalance = wallet.Balance - wallet.HeldBalance;
        if (availableBalance < request.PointsAmount)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure<RedemptionRequestResponse>(RedemptionErrors.InsufficientBalance);
        }

        // 9. Create request & lock points
        var redemptionRequest = new RedemptionRequest
        {
            UserId = userId,
            Method = request.Method,
            Status = status,
            PointsAmount = request.PointsAmount,
            SarRate = sarRate,
            SarAmount = sarAmount,
            Iban = request.Iban,
            AccountNumber = request.AccountNumber,
            Address = request.Address,
            SwiftCode = request.SwiftCode,
            AccountName = request.AccountName
        };

        wallet.HeldBalance += request.PointsAmount;
        wallet.HeldSarBalance += sarAmount;

        await _context.RedemptionRequests.AddAsync(redemptionRequest, ct);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Redemption request {Id} created by user {UserId} for {Points} points",
            redemptionRequest.Id, userId, request.PointsAmount);

        await _notificationService.CreateAsync(userId, NotificationType.RedemptionCreated,
            "طلب استبدال جديد", $"تم تقديم طلب استبدال {request.PointsAmount} نقطة",
            redemptionRequest.Id, ct);

        return Result.Success(MapToResponse(redemptionRequest));
    }

    public async Task<Result<RedemptionRequestResponse?>> GetActiveRequestAsync(
        string userId, CancellationToken ct = default)
    {
        var request = await _context.RedemptionRequests
            .Where(r => r.UserId == userId
                && r.Status != RedemptionRequestStatus.Completed
                && r.Status != RedemptionRequestStatus.Rejected
                && r.Status != RedemptionRequestStatus.Cancelled)
            .FirstOrDefaultAsync(ct);

        return Result.Success(request is null ? null : MapToResponse(request));
    }

    public async Task<Result<PaginatedResult<RedemptionRequestResponse>>> GetHistoryAsync(
        string userId, RedemptionListQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Helpers.PaginationHelper.Normalize(query.Page, query.PageSize);

        var baseQuery = _context.RedemptionRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new PaginatedResult<RedemptionRequestResponse>(
            items.Select(MapToResponse).ToList(),
            totalCount,
            page,
            pageSize);

        return Result.Success(result);
    }

    public async Task<Result<AvailableBalanceResponse>> GetAvailableBalanceAsync(
        string userId, CancellationToken ct = default)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet is null)
            return Result.Success(new AvailableBalanceResponse(0, 0, 0, 0));

        await using var transaction = await _context.BeginTransactionAsync(ct);
        await ExpireOldPointsAsync(wallet, ct);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var available = wallet.Balance - wallet.HeldBalance;
        var settings = await _context.RewardSettings.FirstOrDefaultAsync(ct);
        var sarRate = settings?.PointsToSarRate ?? 10m;
        var availableSar = available / sarRate;

        return Result.Success(new AvailableBalanceResponse(
            wallet.Balance,
            wallet.HeldBalance,
            available,
            availableSar));
    }

    // --- Helpers ---

    internal async Task ExpireOldPointsAsync(Wallet wallet, CancellationToken ct = default)
    {
        var expiryDate = DateTime.UtcNow.AddMonths(-15);

        var expiredTransactions = await _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id
                && t.Type == WalletTransactionType.Earned
                && t.RemainingAmount > 0
                && t.CreatedAt < expiryDate)
            .ToListAsync(ct);

        if (expiredTransactions.Count == 0)
            return;

        // Only expire up to the available (non-held) balance to protect held points
        var maxExpirable = wallet.Balance - wallet.HeldBalance;
        if (maxExpirable <= 0)
            return;

        decimal totalExpired = 0;
        decimal totalSarExpired = 0;

        foreach (var tx in expiredTransactions)
        {
            if (totalExpired >= maxExpirable)
                break;

            var expireAmount = Math.Min(tx.RemainingAmount, maxExpirable - totalExpired);
            var sarExpireAmount = tx.SarRate > 0 ? expireAmount / tx.SarRate : 0;

            totalExpired += expireAmount;
            totalSarExpired += sarExpireAmount;

            await _context.WalletTransactions.AddAsync(new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = -expireAmount,
                Type = WalletTransactionType.Expired,
                ReferenceId = tx.Id,
                Description = "نقاط منتهية الصلاحية",
                SarRate = tx.SarRate,
                SarAmount = -sarExpireAmount
            }, ct);

            tx.RemainingAmount -= expireAmount;
        }

        wallet.Balance -= totalExpired;
        wallet.SarBalance -= totalSarExpired;

        // No separate SaveChangesAsync — caller owns the save/transaction
        _logger.LogInformation("Expired {Points} points for wallet {WalletId}",
            totalExpired, wallet.Id);
    }

    private async Task<City?> GetUserCityAsync(ApplicationUser user)
    {
        if (user.NationalAddress?.CityId is null) return null;

        return await _context.Cities
            .Include(c => c.Region)
            .FirstOrDefaultAsync(c => c.Id == user.NationalAddress.CityId);
    }

    private static RedemptionRequestResponse MapToResponse(RedemptionRequest r) => new(
        r.Id,
        r.Method,
        r.Status,
        r.PointsAmount,
        r.SarRate,
        r.SarAmount,
        r.Iban,
        r.AccountNumber,
        r.Address,
        r.SwiftCode,
        r.AccountName,
        r.CashOtpExpiresAt,
        r.RejectionReason,
        r.CreatedAt
    );
}
