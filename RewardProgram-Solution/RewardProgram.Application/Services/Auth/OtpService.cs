using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Entities.OTP;

namespace RewardProgram.Application.Services.Auth;

public class OtpService : IOtpService
{
    private readonly ITwilioRepository _twilioRepository;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        ITwilioRepository twilioRepository,
        IApplicationDbContext context,
        ILogger<OtpService> logger)
    {
        _twilioRepository = twilioRepository;
        _context = context;
        _logger = logger;
    }

    public async Task<Result<string>> SendAsync(string mobileNumber, string? registrationData = null)
    {
        // Rate limiting check
        var rateLimitResult = await CheckRateLimitAsync(mobileNumber);
        if (rateLimitResult.IsFailure)
        {
            return Result.Failure<string>(rateLimitResult.Error);
        }

        var result = await _twilioRepository.SendOtpAsync(mobileNumber);

        if (result.IsFailure)
        {
            return result;
        }

        var pinId = result.Value;

        // Store OTP record in database
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

        await _context.OtpCodes.AddAsync(otpCode);
        await _context.SaveChangesAsync();

        _logger.LogInformation("OTP sent and stored for PinId: {PinId}, Mobile: {Mobile}",
            pinId, MaskMobileNumber(mobileNumber));

        return Result.Success(pinId);
    }

    public async Task<Result<string?>> VerifyAsync(string pinId, string otp)
    {
        // Find OTP record (not used yet)
        var otpCode = await _context.OtpCodes
            .FirstOrDefaultAsync(o => o.PinId == pinId && !o.IsUsed);

        if (otpCode == null)
        {
            _logger.LogWarning("OTP record not found for PinId: {PinId}", pinId);
            return Result.Failure<string?>(AuthErrors.OtpNotFound);
        }

        // Check if OTP has expired
        if (otpCode.IsExpired)
        {
            _logger.LogWarning("OTP expired for PinId: {PinId}", pinId);
            return Result.Failure<string?>(AuthErrors.OtpExpired);
        }

        // Check if too many verification attempts
        if (otpCode.VerificationAttempts >= OtpCode.MaxVerificationAttempts)
        {
            _logger.LogWarning("Max verification attempts exceeded for PinId: {PinId}", pinId);
            return Result.Failure<string?>(AuthErrors.TooManyOtpRequests);
        }

        // Increment verification attempts before verifying
        otpCode.VerificationAttempts++;
        await _context.SaveChangesAsync();

        // Verify with Twilio
        var result = await _twilioRepository.VerifyOtpAsync(pinId, otp);

        if (result.IsFailure)
        {
            return Result.Failure<string?>(result.Error);
        }

        if (!result.Value)
        {
            _logger.LogWarning("Invalid OTP for PinId: {PinId}, Attempts: {Attempts}/{Max}",
                pinId, otpCode.VerificationAttempts, OtpCode.MaxVerificationAttempts);
            return Result.Failure<string?>(AuthErrors.OtpInvalid);
        }

        // Mark as used
        otpCode.IsUsed = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("OTP verified successfully for PinId: {PinId}", pinId);

        return Result.Success(otpCode.RegistrationData);
    }

    private async Task<Result> CheckRateLimitAsync(string mobileNumber)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-OtpCode.RateLimitWindowMinutes);

        var recentRequestCount = await _context.OtpCodes
            .CountAsync(o => o.MobileNumber == mobileNumber && o.CreatedAt >= windowStart);

        if (recentRequestCount >= OtpCode.MaxRequestsPerWindow)
        {
            _logger.LogWarning(
                "Rate limit exceeded for mobile: {Mobile}. {Count} requests in last {Window} minutes",
                MaskMobileNumber(mobileNumber),
                recentRequestCount,
                OtpCode.RateLimitWindowMinutes);

            return Result.Failure(AuthErrors.TooManyOtpRequests);
        }

        return Result.Success();
    }

    private static string MaskMobileNumber(string mobileNumber)
    {
        if (string.IsNullOrEmpty(mobileNumber) || mobileNumber.Length < 4)
            return "****";

        return $"{mobileNumber[..3]}****{mobileNumber[^3..]}";
    }
}