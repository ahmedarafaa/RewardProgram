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
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services;

public class RedemptionService : IRedemptionService
{
    private const string CashOtpTemplateSid = "HX345d6229ba79de58919c1daab1d1dfbc";

    // Cash OTP lifetime: 72 hours. Previously 14 days, which combined with
    // attempts-reset-on-resend gave attackers a multi-week brute-force window.
    // 72h covers normal handover scheduling + weekend without enabling abuse.
    private static readonly TimeSpan CashOtpLifetime = TimeSpan.FromHours(72);

    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ITwilioService _twilioService;
    private readonly ILogger<RedemptionService> _logger;

    public RedemptionService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        ITwilioService twilioService,
        ILogger<RedemptionService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _twilioService = twilioService;
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

        // For cash redemptions, pre-issue the handover OTP so the user receives it
        // on WhatsApp up-front; it stays valid through the full approval cycle.
        string? cashOtp = null;
        if (request.Method == RedemptionMethod.Cash)
        {
            cashOtp = GenerateOtp();
            redemptionRequest.CashOtpHash = HashOtp(cashOtp);
            redemptionRequest.CashOtpExpiresAt = DateTime.UtcNow.Add(CashOtpLifetime);
            redemptionRequest.CashOtpAttempts = 0;
        }

        wallet.HeldBalance += request.PointsAmount;
        wallet.HeldSarBalance += sarAmount;

        await _context.RedemptionRequests.AddAsync(redemptionRequest, ct);
        await _context.SaveChangesAsync(ct);

        // Send WhatsApp OTP BEFORE commit. If delivery fails, roll back so the user
        // never sees a "created" request they can't act on.
        if (cashOtp is not null && !string.IsNullOrEmpty(user.MobileNumber))
        {
            var variables = new Dictionary<string, string>
            {
                { "1", cashOtp },
                { "2", request.PointsAmount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) },
                { "3", sarAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) }
            };
            var sendResult = await _twilioService.SendWhatsAppMessageAsync(
                user.MobileNumber, CashOtpTemplateSid, variables, ct);

            if (sendResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogWarning(
                    "Cash OTP WhatsApp send failed for user {UserId}; redemption request rolled back. Error: {Error}",
                    userId, sendResult.Error.Code);
                return Result.Failure<RedemptionRequestResponse>(sendResult.Error);
            }
        }

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Redemption request {Id} created by user {UserId} for {Points} points",
            redemptionRequest.Id, userId, request.PointsAmount);

        // No self-notification — the user sees the new request in their own response/UI.

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

    public async Task<Result> ResendCashOtpAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null || user.IsDisabled || user.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure(RedemptionErrors.UserNotApproved);

        if (string.IsNullOrEmpty(user.MobileNumber))
            return Result.Failure(RedemptionErrors.OtpSendFailed);

        var redemptionRequest = await _context.RedemptionRequests
            .Where(r => r.UserId == userId
                && r.Method == RedemptionMethod.Cash
                && r.Status != RedemptionRequestStatus.Completed
                && r.Status != RedemptionRequestStatus.Rejected
                && r.Status != RedemptionRequestStatus.Cancelled)
            .FirstOrDefaultAsync(ct);

        if (redemptionRequest is null)
            return Result.Failure(RedemptionErrors.NoActiveCashRequest);

        // Cooldown: 30s since last mutation (UpdatedAt). Prevents accidental double-taps
        // and spam without requiring a dedicated last-sent-at column.
        var lastTouch = redemptionRequest.UpdatedAt ?? redemptionRequest.CreatedAt;
        if (DateTime.UtcNow - lastTouch < TimeSpan.FromSeconds(30))
            return Result.Failure(RedemptionErrors.ResendCooldown);

        var newOtp = GenerateOtp();
        redemptionRequest.CashOtpHash = HashOtp(newOtp);
        redemptionRequest.CashOtpExpiresAt = DateTime.UtcNow.Add(CashOtpLifetime);
        redemptionRequest.CashOtpAttempts = 0;

        await using var transaction = await _context.BeginTransactionAsync(ct);
        await _context.SaveChangesAsync(ct);

        var variables = new Dictionary<string, string>
        {
            { "1", newOtp },
            { "2", redemptionRequest.PointsAmount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) },
            { "3", redemptionRequest.SarAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) }
        };
        var sendResult = await _twilioService.SendWhatsAppMessageAsync(
            user.MobileNumber, CashOtpTemplateSid, variables, ct);

        if (sendResult.IsFailure)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(
                "Cash OTP resend failed for user {UserId}, request {RequestId}. Error: {Error}",
                userId, redemptionRequest.Id, sendResult.Error.Code);
            return Result.Failure(RedemptionErrors.OtpSendFailed);
        }

        await transaction.CommitAsync(ct);
        _logger.LogInformation("Cash OTP resent for request {RequestId}", redemptionRequest.Id);

        return Result.Success();
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
                DescriptionKey = "Wallet.Tx.PointsExpired",
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

    private static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexStringLower(bytes);
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
