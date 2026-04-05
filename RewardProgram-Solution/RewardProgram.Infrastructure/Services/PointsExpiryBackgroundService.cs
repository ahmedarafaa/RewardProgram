using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Infrastructure.Services;

public class PointsExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PointsExpiryBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public PointsExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PointsExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PointsExpiryBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredPointsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired points");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredPointsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var expiryDate = DateTime.UtcNow.AddMonths(-15);

        // Find all wallets that have expired earned transactions
        var expiredTransactions = await context.WalletTransactions
            .Where(t => t.Type == WalletTransactionType.Earned
                && t.RemainingAmount > 0
                && t.CreatedAt < expiryDate)
            .Include(t => t.Wallet)
            .ToListAsync(ct);

        if (expiredTransactions.Count == 0)
        {
            _logger.LogDebug("No expired points found");
            return;
        }

        // Group by wallet for batch processing
        var walletGroups = expiredTransactions.GroupBy(t => t.WalletId);

        foreach (var group in walletGroups)
        {
            var wallet = group.First().Wallet;
            decimal totalExpiredPoints = 0;
            decimal totalExpiredSar = 0;

            foreach (var tx in group)
            {
                totalExpiredPoints += tx.RemainingAmount;

                // Calculate SAR per-transaction using its own rate (guard against zero)
                var txSarAmount = tx.SarRate > 0 ? tx.RemainingAmount / tx.SarRate : 0;
                totalExpiredSar += txSarAmount;

                await context.WalletTransactions.AddAsync(new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Amount = -tx.RemainingAmount,
                    Type = WalletTransactionType.Expired,
                    ReferenceId = tx.Id,
                    Description = "نقاط منتهية ا��صلاحية",
                    SarRate = tx.SarRate,
                    SarAmount = -txSarAmount
                }, ct);

                tx.RemainingAmount = 0;
            }

            wallet.Balance -= totalExpiredPoints;
            wallet.SarBalance -= totalExpiredSar;

            _logger.LogInformation("Expired {Points} points for wallet {WalletId} (user {UserId})",
                totalExpiredPoints, wallet.Id, wallet.UserId);
        }

        await context.SaveChangesAsync(ct);

        _logger.LogInformation("Processed {Count} expired transactions across {Wallets} wallets",
            expiredTransactions.Count, walletGroups.Count());
    }
}
