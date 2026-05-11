using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;

namespace RewardProgram.Application.Services.Auth;

public class OtpFallbackService : IOtpFallbackService
{
    private readonly IApplicationDbContext _context;
    private readonly ITwilioService _twilioService;
    private readonly ILogger<OtpFallbackService> _logger;

    public OtpFallbackService(
        IApplicationDbContext context,
        ITwilioService twilioService,
        ILogger<OtpFallbackService> logger)
    {
        _context = context;
        _twilioService = twilioService;
        _logger = logger;
    }

    public async Task<Result<bool>> TriggerSmsForFailedVerificationAsync(
        string verificationSid, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(verificationSid))
            return Result.Success(false);

        // Lookup by CurrentSid — the active Twilio handle. PinId would also work
        // here on the initial WhatsApp send (they're equal), but CurrentSid is
        // semantically correct: this is the Sid Twilio is reporting on.
        var row = await _context.OtpCodes
            .FirstOrDefaultAsync(o => o.CurrentSid == verificationSid, ct);

        // The four "do nothing" cases — all are normal and not error conditions:
        //   1. Row not found (webhook for an unknown Sid — could be from a Twilio
        //      replay or a Sid issued before our row was saved).
        //   2. Already used (user has verified — no action needed).
        //   3. Already fallen back (SMS already sent for this row).
        //   4. Expired (OTP lifetime ran out — let the user /resend-otp instead).
        if (row is null)
        {
            _logger.LogInformation(
                "Twilio webhook: no OtpCode found for Sid {Sid}. Likely already verified or pre-feature row.",
                verificationSid);
            return Result.Success(false);
        }

        if (row.IsUsed)
            return Result.Success(false);

        if (row.FallbackFired)
            return Result.Success(false);

        if (row.IsExpired)
        {
            _logger.LogInformation(
                "Twilio webhook for {Mobile}: skipping SMS — OTP has already expired.",
                MobileNumberHelper.Mask(row.MobileNumber));
            row.FallbackFired = true;   // prevent re-processing on a retry
            await _context.SaveChangesAsync(ct);
            return Result.Success(false);
        }

        // Fire the SMS Verify for the same number. Mark FallbackFired BEFORE the
        // network call so a concurrent webhook (Twilio retries on non-2xx) can't
        // race us into sending twice. If SMS itself fails we leave FallbackFired
        // = true — user can /resend-otp; we don't want a tight retry loop.
        row.FallbackFired = true;
        await _context.SaveChangesAsync(ct);

        var smsResult = await _twilioService.SendOtpAsync(row.MobileNumber, "sms", ct);
        if (smsResult.IsFailure)
        {
            _logger.LogError(
                "SMS fallback (webhook) failed for {Mobile} (PinId={PinId}): {Error}",
                MobileNumberHelper.Mask(row.MobileNumber),
                row.PinId,
                smsResult.Error.Code);
            return Result.Failure<bool>(smsResult.Error);
        }

        row.CurrentSid = smsResult.Value;
        row.Channel = "sms";
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SMS fallback (webhook) fired for {Mobile} (PinId={PinId}, NewSid={NewSid})",
            MobileNumberHelper.Mask(row.MobileNumber),
            row.PinId,
            smsResult.Value);

        return Result.Success(true);
    }
}
