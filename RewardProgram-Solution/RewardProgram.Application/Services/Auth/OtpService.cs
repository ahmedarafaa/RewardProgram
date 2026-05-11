using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Entities.OTP;

namespace RewardProgram.Application.Services.Auth;

public class OtpService : IOtpService
{
    private readonly ITwilioService _twilioService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        ITwilioService twilioService,
        IApplicationDbContext context,
        ILogger<OtpService> logger)
    {
        _twilioService = twilioService;
        _context = context;
        _logger = logger;
    }

    public async Task<Result<string>> SendAsync(string mobileNumber, string? registrationData = null, CancellationToken ct = default)
    {
        mobileNumber = MobileNumberHelper.Normalize(mobileNumber);
        var rateLimitResult = await CheckRateLimitAsync(mobileNumber, ct);
        if (rateLimitResult.IsFailure)
            return Result.Failure<string>(rateLimitResult.Error);

        var sendResult = await SendWithSyncFallbackAsync(mobileNumber, primaryChannel: "whatsapp", ct);
        if (sendResult.IsFailure)
            return Result.Failure<string>(sendResult.Error);

        var (pinId, channel, fallbackFired) = sendResult.Value;

        var otpCode = new OtpCode
        {
            PinId = pinId,
            CurrentSid = pinId,
            Channel = channel,
            FallbackFired = fallbackFired,
            MobileNumber = mobileNumber,
            RegistrationData = registrationData,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpCode.DefaultExpirationMinutes),
            IsUsed = false,
            VerificationAttempts = 0
        };

        await _context.OtpCodes.AddAsync(otpCode, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("OTP sent and stored for Mobile: {Mobile} via {Channel}",
            MobileNumberHelper.Mask(mobileNumber), channel);

        return Result.Success(pinId);
    }

    public async Task<Result<SendOtpResponse>> ResendAsync(string mobileNumber, CancellationToken ct = default)
    {
        mobileNumber = MobileNumberHelper.Normalize(mobileNumber);

        // 1. Find the most recent OTP for this mobile
        var lastOtp = await _context.OtpCodes
            .Where(o => o.MobileNumber == mobileNumber)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastOtp == null)
            return Result.Failure<SendOtpResponse>(AuthErrors.OtpNotFound);

        // 2. Check 30-second cooldown
        var secondsSinceLast = (DateTime.UtcNow - lastOtp.CreatedAt).TotalSeconds;
        if (secondsSinceLast < OtpCode.ResendCooldownSeconds)
            return Result.Failure<SendOtpResponse>(AuthErrors.OtpResendTooSoon);

        // 3. Check global rate limit
        var rateLimitResult = await CheckRateLimitAsync(mobileNumber, ct);
        if (rateLimitResult.IsFailure)
            return Result.Failure<SendOtpResponse>(rateLimitResult.Error);

        // 4. Mark old OTP as used (invalidate it)
        if (!lastOtp.IsUsed)
        {
            lastOtp.IsUsed = true;
            await _context.SaveChangesAsync(ct);
        }

        // 5. Send new OTP — preferring SMS this time. The user pressed Resend,
        //    which is a strong signal that the first attempt (WhatsApp) didn't
        //    reach them. Going straight to SMS guarantees they get the code via
        //    a different channel rather than retrying the same one. If SMS fails
        //    synchronously (rare), the helper falls back to WhatsApp.
        var sendResult = await SendWithSyncFallbackAsync(mobileNumber, primaryChannel: "sms", ct);
        if (sendResult.IsFailure)
            return Result.Failure<SendOtpResponse>(sendResult.Error);

        var (newPinId, channel, fallbackFired) = sendResult.Value;

        var newOtp = new OtpCode
        {
            PinId = newPinId,
            CurrentSid = newPinId,
            Channel = channel,
            FallbackFired = fallbackFired,
            MobileNumber = mobileNumber,
            RegistrationData = lastOtp.RegistrationData,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpCode.DefaultExpirationMinutes),
            IsUsed = false,
            VerificationAttempts = 0
        };

        await _context.OtpCodes.AddAsync(newOtp, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("OTP resent for Mobile: {Mobile}, NewPinId: {NewPinId}, Channel: {Channel}",
            MobileNumberHelper.Mask(mobileNumber), newPinId, channel);

        return Result.Success(new SendOtpResponse(
            PinId: newPinId,
            MaskedMobileNumber: MobileNumberHelper.Mask(mobileNumber)
        ));
    }

    public async Task<Result<OtpVerificationResult>> VerifyAsync(string pinId, string otp, CancellationToken ct = default)
    {
        var otpCode = await _context.OtpCodes
            .FirstOrDefaultAsync(o => o.PinId == pinId && !o.IsUsed, ct);

        if (otpCode == null)
        {
            _logger.LogWarning("OTP record not found for PinId: {PinId}", pinId);
            return Result.Failure<OtpVerificationResult>(AuthErrors.OtpNotFound);
        }

        if (otpCode.IsExpired)
        {
            _logger.LogWarning("OTP expired for PinId: {PinId}", pinId);
            return Result.Failure<OtpVerificationResult>(AuthErrors.OtpExpired);
        }

        if (otpCode.VerificationAttempts >= OtpCode.MaxVerificationAttempts)
        {
            _logger.LogWarning("Max verification attempts exceeded for PinId: {PinId}", pinId);
            return Result.Failure<OtpVerificationResult>(AuthErrors.MaxVerificationAttemptsExceeded);
        }

        otpCode.VerificationAttempts++;

        // Verify against CurrentSid — may have been rotated to the SMS-channel
        // Sid by the Twilio delivery webhook. PinId stays as the public lookup
        // token. Legacy rows from before this feature have CurrentSid empty; fall
        // back to PinId for them so existing pending OTPs remain verifiable.
        var sidToVerify = string.IsNullOrEmpty(otpCode.CurrentSid) ? pinId : otpCode.CurrentSid;
        var result = await _twilioService.VerifyOtpAsync(sidToVerify, otp, ct);
        if (result.IsFailure)
        {
            await _context.SaveChangesAsync(ct);
            return Result.Failure<OtpVerificationResult>(result.Error);
        }

        if (!result.Value)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogWarning("Invalid OTP for PinId: {PinId}, Attempts: {Attempts}/{Max}",
                pinId, otpCode.VerificationAttempts, OtpCode.MaxVerificationAttempts);
            return Result.Failure<OtpVerificationResult>(AuthErrors.OtpInvalid);
        }

        otpCode.IsUsed = true;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("OTP verified successfully for PinId: {PinId}", pinId);

        return Result.Success(new OtpVerificationResult(
            MobileNumber: otpCode.MobileNumber,
            RegistrationData: otpCode.RegistrationData
        ));
    }

    // Try primaryChannel first; if Twilio surfaces a synchronous error (number
    // not channel-capable, channel not enabled, 5xx, etc.), retry the other
    // channel in the same request so the user gets exactly ONE OTP — through
    // whichever channel works.
    //
    // Used in two flows:
    //   - SendAsync (first attempt) uses primaryChannel="whatsapp" — cheaper /
    //     better UX, but falls back to SMS on Twilio errors.
    //   - ResendAsync (user pressed Resend) uses primaryChannel="sms" — strong
    //     signal that the WhatsApp attempt didn't reach the user, so switch
    //     channel rather than retry the same one.
    //
    // FallbackFired is set when the channel actually used differs from the
    // requested primary — i.e. "we already used up the in-request fallback".
    private async Task<Result<(string Sid, string Channel, bool FallbackFired)>> SendWithSyncFallbackAsync(
        string mobileNumber, string primaryChannel, CancellationToken ct)
    {
        var fallbackChannel = primaryChannel == "whatsapp" ? "sms" : "whatsapp";

        var primary = await _twilioService.SendOtpAsync(mobileNumber, primaryChannel, ct);
        if (primary.IsSuccess)
            return Result.Success((primary.Value, primaryChannel, FallbackFired: false));

        _logger.LogWarning(
            "{Channel} send failed synchronously for {Mobile} ({Error}). Retrying via {Fallback}.",
            primaryChannel,
            MobileNumberHelper.Mask(mobileNumber),
            primary.Error.Code,
            fallbackChannel);

        var secondary = await _twilioService.SendOtpAsync(mobileNumber, fallbackChannel, ct);
        if (secondary.IsFailure)
        {
            _logger.LogError(
                "Both {Primary} and {Fallback} sends failed for {Mobile}. {Primary}={PrimaryErr}, {Fallback}={FallbackErr}",
                primaryChannel, fallbackChannel,
                MobileNumberHelper.Mask(mobileNumber),
                primaryChannel, primary.Error.Code,
                fallbackChannel, secondary.Error.Code);
            return Result.Failure<(string, string, bool)>(secondary.Error);
        }

        return Result.Success((secondary.Value, fallbackChannel, FallbackFired: true));
    }

    private async Task<Result> CheckRateLimitAsync(string mobileNumber, CancellationToken ct = default)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-OtpCode.RateLimitWindowMinutes);

        var recentRequestCount = await _context.OtpCodes
            .CountAsync(o => o.MobileNumber == mobileNumber && o.CreatedAt >= windowStart, ct);

        if (recentRequestCount >= OtpCode.MaxRequestsPerWindow)
        {
            _logger.LogWarning(
                "Rate limit exceeded for mobile: {Mobile}. {Count} requests in last {Window} minutes",
                MobileNumberHelper.Mask(mobileNumber),
                recentRequestCount,
                OtpCode.RateLimitWindowMinutes);

            return Result.Failure(AuthErrors.TooManyOtpRequests);
        }

        return Result.Success();
    }
}
