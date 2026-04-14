using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NanoidDotNet;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Invitation;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services;

public class InvitationService : IInvitationService
{
    private const string InviteAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int InviteCodeLength = 8;
    private const int MaxRewardedInvitations = 20;
    private const string ShareBaseUrl = "https://app.raedrewardapp.com/invite/";

    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly INotificationService _notificationService;
    private readonly ILogger<InvitationService> _logger;

    public InvitationService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        IQrCodeGenerator qrCodeGenerator,
        INotificationService notificationService,
        ILogger<InvitationService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _qrCodeGenerator = qrCodeGenerator;
        _notificationService = notificationService;
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

        var shareLink = $"{ShareBaseUrl}{user.InvitationCode}";
        var qrBytes = _qrCodeGenerator.Generate(shareLink);
        var qrBase64 = Convert.ToBase64String(qrBytes);

        // Stats: count users who were invited by this user
        var invitedUsers = await _userRepository.Query()
            .Where(u => u.InvitedByUserId == userId)
            .Select(u => new { u.RegistrationStatus })
            .ToListAsync(ct);

        var totalInvitations = invitedUsers.Count;
        var approvedInvitations = invitedUsers
            .Count(u => u.RegistrationStatus == Domain.Enums.UserEnums.RegistrationStatus.Approved);

        // Total points earned from invitations
        var totalPointsEarned = await _context.WalletTransactions
            .Where(t => t.Wallet.UserId == userId && t.Type == WalletTransactionType.InvitationReward)
            .SumAsync(t => t.Amount, ct);

        return Result.Success(new InvitationInfoResponse(
            user.InvitationCode,
            shareLink,
            qrBase64,
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
                var inviteeWallet = await GetOrCreateWalletAsync(invitee.Id, ct);
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

            // Credit inviter only if under 20 rewarded invitations
            var inviterRewardCount = 0;
            var inviterRewarded = false;
            if (inviterPoints > 0)
            {
                // Count only inviter rewards (where ReferenceId is an invitee's userId, not the inviter's own invitee reward)
                inviterRewardCount = await _context.WalletTransactions
                    .CountAsync(t => t.Wallet.UserId == inviter.Id
                        && t.Type == WalletTransactionType.InvitationReward
                        && t.ReferenceId != inviter.Id, ct);

                if (inviterRewardCount < MaxRewardedInvitations)
                {
                    var inviterWallet = await GetOrCreateWalletAsync(inviter.Id, ct);
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
                        "Invitation reward: {Points} points credited to inviter {InviterId} (reward #{Count})",
                        inviterPoints, inviter.Id, inviterRewardCount + 1);
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

    private async Task<Wallet> GetOrCreateWalletAsync(string userId, CancellationToken ct)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        if (wallet is not null)
            return wallet;

        wallet = new Wallet
        {
            UserId = userId,
            Balance = 0
        };

        // Note: If concurrent calls race to create the wallet, the unique constraint
        // on UserId will cause DbUpdateException. The caller (CreditInvitationRewardsAsync)
        // catches DbUpdateException and logs it as a duplicate — safe.
        await _context.Wallets.AddAsync(wallet, ct);
        return wallet;
    }
}
