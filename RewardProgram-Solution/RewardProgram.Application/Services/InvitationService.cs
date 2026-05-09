using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NanoidDotNet;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Invitation;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services;

public class InvitationService : IInvitationService
{
    private const string InviteAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int InviteCodeLength = 8;
    private const int MaxRewardedInvitations = 20;

    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly InvitationOptions _options;
    private readonly ILogger<InvitationService> _logger;

    public InvitationService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        INotificationService notificationService,
        IOptions<InvitationOptions> options,
        ILogger<InvitationService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<InvitationInfoResponse>> GetInvitationInfoAsync(
        string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<InvitationInfoResponse>(Errors.InvitationErrors.InviterNotApproved);

        // Lazy-generate invitation code if missing
        if (string.IsNullOrEmpty(user.InvitationCode))
        {
            user.InvitationCode = await GenerateUniqueCodeAsync(ct);
            await _userRepository.UpdateAsync(user);
        }

        var shareLink = $"{_options.ShareBaseUrl}{user.InvitationCode}";

        // Stats: count users who were invited by this user
        var invitedUsers = await _userRepository.Query()
            .Where(u => u.InvitedByUserId == userId)
            .Select(u => new { u.RegistrationStatus })
            .ToListAsync(ct);

        var totalInvitations = invitedUsers.Count;
        var approvedInvitations = invitedUsers
            .Count(u => u.RegistrationStatus == Domain.Enums.UserEnums.RegistrationStatus.Approved);

        // Total points earned from inviting OTHERS — excludes this user's own signup
        // bonus (which is also stored as an InvitationReward but with ReferenceId
        // pointing to their inviter, not to someone they invited).
        var totalPointsEarned = await _context.WalletTransactions
            .Where(t => t.Wallet.UserId == userId
                && t.Type == WalletTransactionType.InvitationReward
                && t.ReferenceId != null
                && t.ReferenceId != user.InvitedByUserId)
            .SumAsync(t => t.Amount, ct);

        return Result.Success(new InvitationInfoResponse(
            user.InvitationCode,
            shareLink,
            totalInvitations,
            approvedInvitations,
            totalPointsEarned
        ));
    }

    public async Task CreditInvitationRewardsAsync(string invitedUserId, CancellationToken ct = default)
    {
        var invitee = await _userRepository.FindByIdAsync(invitedUserId, ct);
        if (invitee?.InvitedByUserId is null)
            return;

        var inviter = await _userRepository.FindByIdAsync(invitee.InvitedByUserId, ct);
        if (inviter is null)
            return;

        var settings = await _context.RewardSettings.FirstOrDefaultAsync(ct);
        var inviterPoints = settings?.InviterRewardPoints ?? 100m;
        var inviteePoints = settings?.InviteeRewardPoints ?? 50m;
        var sarRate = settings?.PointsToSarRate ?? 10m;

        // Skip inviter reward if they are disabled, self-deleted, or no longer approved.
        // Invitee still gets their signup reward — it's a user acquisition incentive.
        var inviterEligible = !inviter.IsDisabled
            && !inviter.IsAccountDeleted
            && inviter.RegistrationStatus == RegistrationStatus.Approved;

        if (!inviterEligible)
        {
            _logger.LogInformation(
                "Inviter {InviterId} is not eligible for reward (disabled/deleted/not-approved) — invitee {InviteeId} will still receive signup reward",
                inviter.Id, invitee.Id);
            inviterPoints = 0m;
        }

        // Pre-create wallets outside the main transaction so the unique-constraint
        // race on first-ever-credit is resolved here rather than poisoning the
        // change tracker inside the crediting transaction.
        if (inviteePoints > 0)
            await EnsureWalletAsync(invitee.Id, ct);
        if (inviterPoints > 0)
            await EnsureWalletAsync(inviter.Id, ct);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            // Idempotency guard: check if rewards were already credited for this pair
            var alreadyCredited = await _context.WalletTransactions
                .AnyAsync(t => t.Type == WalletTransactionType.InvitationReward
                    && t.ReferenceId == inviter.Id
                    && t.Wallet.UserId == invitee.Id, ct);

            if (alreadyCredited)
            {
                _logger.LogInformation("Invitation rewards already credited for invitee {InviteeId}, skipping", invitedUserId);
                return;
            }

            // Always credit invitee
            if (inviteePoints > 0)
            {
                var inviteeWallet = await _context.Wallets.FirstAsync(w => w.UserId == invitee.Id, ct);
                var inviteeSarAmount = inviteePoints / sarRate;
                inviteeWallet.Balance += inviteePoints;
                inviteeWallet.SarBalance += inviteeSarAmount;

                await _context.WalletTransactions.AddAsync(new WalletTransaction
                {
                    WalletId = inviteeWallet.Id,
                    Amount = inviteePoints,
                    Type = WalletTransactionType.InvitationReward,
                    ReferenceId = inviter.Id,
                    Description = $"مكافأة تسجيل عبر دعوة من {inviter.Name}",
                    SarRate = sarRate,
                    SarAmount = inviteeSarAmount,
                    RemainingAmount = inviteePoints
                }, ct);

                _logger.LogInformation(
                    "Invitation reward: {Points} points credited to invitee {InviteeId}",
                    inviteePoints, invitee.Id);
            }

            // Credit inviter only if under the 20 rewarded invitations cap.
            // Reserve the slot with an atomic UPDATE that only succeeds when the
            // current counter is below the cap — this serializes the cap check
            // against concurrent approvals without a table lock.
            var inviterRewarded = false;
            if (inviterPoints > 0)
            {
                var reserved = await _userRepository.Query()
                    .Where(u => u.Id == inviter.Id && u.InviterRewardCount < MaxRewardedInvitations)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.InviterRewardCount, u => u.InviterRewardCount + 1), ct);

                if (reserved == 1)
                {
                    var inviterWallet = await _context.Wallets.FirstAsync(w => w.UserId == inviter.Id, ct);
                    var inviterSarAmount = inviterPoints / sarRate;
                    inviterWallet.Balance += inviterPoints;
                    inviterWallet.SarBalance += inviterSarAmount;

                    await _context.WalletTransactions.AddAsync(new WalletTransaction
                    {
                        WalletId = inviterWallet.Id,
                        Amount = inviterPoints,
                        Type = WalletTransactionType.InvitationReward,
                        ReferenceId = invitee.Id,
                        Description = $"مكافأة دعوة — {invitee.Name}",
                        SarRate = sarRate,
                        SarAmount = inviterSarAmount,
                        RemainingAmount = inviterPoints
                    }, ct);

                    inviterRewarded = true;

                    _logger.LogInformation(
                        "Invitation reward: {Points} points credited to inviter {InviterId}",
                        inviterPoints, inviter.Id);
                }
                else
                {
                    _logger.LogInformation(
                        "Inviter {InviterId} has reached max {Max} invitation rewards — skipping inviter reward",
                        inviter.Id, MaxRewardedInvitations);
                }
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Invitee is not notified here — the signup reward is surfaced in the
            // registration response / wallet balance, not as a push notification.

            // Notify inviter (only if reward was actually credited)
            if (inviterRewarded)
            {
                await _notificationService.CreateAsync(inviter.Id, NotificationType.InvitationReward,
                    "مكافأة دعوة", $"حصلت على {inviterPoints} نقطة مكافأة دعوة {invitee.Name}", ct: ct);
            }
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict crediting invitation rewards for invitee {InviteeId} — likely duplicate", invitedUserId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to credit invitation rewards for invitee {InviteeId}", invitedUserId);
            throw;
        }
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = Nanoid.Generate(InviteAlphabet, InviteCodeLength);
            var exists = await _userRepository.Query()
                .AnyAsync(u => u.InvitationCode == code, ct);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Failed to generate unique invitation code after 10 attempts");
    }

    private async Task EnsureWalletAsync(string userId, CancellationToken ct)
    {
        if (await _context.Wallets.AnyAsync(w => w.UserId == userId, ct))
            return;

        var wallet = new Wallet
        {
            UserId = userId,
            Balance = 0
        };

        await _context.Wallets.AddAsync(wallet, ct);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent creator won the unique-index race — detach the failed
            // insert so it doesn't get replayed by the next SaveChanges.
            // Remove() on an Added entity detaches rather than marks Deleted.
            _context.Wallets.Remove(wallet);
        }
    }
}
