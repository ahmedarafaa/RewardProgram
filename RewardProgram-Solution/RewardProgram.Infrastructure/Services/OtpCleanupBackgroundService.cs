using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

public class OtpCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OtpCleanupBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public OtpCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OtpCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OtpCleanupBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredOtpCodesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired OTP codes");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupExpiredOtpCodesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var cutoff = DateTime.UtcNow - RetentionPeriod;

        var deletedCount = await context.OtpCodes
            .Where(o => o.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
            _logger.LogInformation("Cleaned up {Count} expired OTP codes older than {Days} days",
                deletedCount, RetentionPeriod.Days);
    }
}
