using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Options;

namespace RewardProgram.Infrastructure.Services.OtpFallback;

/// Scans for OTP rows whose WhatsApp Verify hasn't been completed within the
/// configured window and issues an SMS Verify for the same number. Rotates the
/// row's CurrentSid to the new Sid so subsequent verify calls hit the SMS code;
/// PinId stays the same so the mobile app contract is untouched.
public class OtpFallbackWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OtpFallbackOptions _options;
    private readonly ILogger<OtpFallbackWorker> _logger;

    public OtpFallbackWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OtpFallbackOptions> options,
        ILogger<OtpFallbackWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("OtpFallbackWorker disabled by configuration — exiting.");
            return;
        }

        _logger.LogInformation(
            "OtpFallbackWorker started. PollInterval={Poll}s, FallbackDelay={Delay}s, BatchSize={Batch}",
            _options.PollIntervalSeconds, _options.FallbackDelaySeconds, _options.BatchSize);

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRowsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OtpFallbackWorker iteration failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessDueRowsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var twilio = scope.ServiceProvider.GetRequiredService<ITwilioService>();

        var now = DateTime.UtcNow;

        // The composite IX_OtpCodes_FallbackDue index covers this predicate.
        // We also exclude expired rows — no point firing SMS for a row the user
        // can no longer verify.
        var due = await db.OtpCodes
            .Where(o => !o.FallbackFired
                     && !o.IsUsed
                     && o.Channel == "whatsapp"
                     && o.FallbackEligibleAt != null
                     && o.FallbackEligibleAt <= now
                     && o.ExpiresAt > now)
            .OrderBy(o => o.FallbackEligibleAt)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(ct);

        if (due.Count == 0)
            return;

        foreach (var row in due)
        {
            if (ct.IsCancellationRequested) break;

            var smsResult = await twilio.SendOtpAsync(row.MobileNumber, "sms", ct);

            // Mark as fired regardless of outcome — if SMS itself failed, we
            // don't want to retry every 5s for the remainder of the OTP lifetime.
            // The user can hit /resend-otp to recover; rate limiting still applies.
            row.FallbackFired = true;

            if (smsResult.IsFailure)
            {
                _logger.LogWarning(
                    "SMS fallback failed for {Mobile} (PinId={PinId}): {ErrorCode}",
                    MobileNumberHelper.Mask(row.MobileNumber),
                    row.PinId,
                    smsResult.Error.Code);
                continue;
            }

            row.CurrentSid = smsResult.Value;
            row.Channel = "sms";

            _logger.LogInformation(
                "SMS fallback fired for {Mobile} (PinId={PinId}, NewSid={NewSid})",
                MobileNumberHelper.Mask(row.MobileNumber),
                row.PinId,
                smsResult.Value);
        }

        await db.SaveChangesAsync(ct);
    }
}
