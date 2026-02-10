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
        var rateLimitResult = await CheckRateLimitAsync(mobileNumber, ct);
        if (rateLimitResult.IsFailure)
            return Result.Failure<string>(rateLimitResult.Error);

        var result = await _twilioService.SendOtpAsync(mobileNumber, ct);
        if (result.IsFailure)
            return result;

        var pinId = result.Value;

        var otpCode = new OtpCode
        {
            PinId = pinId,
            MobileNumber = mobileNumber,
            RegistrationData = registrationData,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpCode.DefaultExpirationMinutes),
            IsUsed = false,
            VerificationAttempts = 0
        };

        await _context.OtpCodes.AddAsync(otpCode, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("OTP sent and stored for PinId: {PinId}, Mobile: {Mobile}",
            pinId, MobileNumberHelper.Mask(mobileNumber));

        return Result.Success(pinId);
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

        var result = await _twilioService.VerifyOtpAsync(pinId, otp, ct);
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
