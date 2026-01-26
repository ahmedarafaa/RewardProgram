using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Entities.OTP;
using RewardProgram.Domain.Enums;
using System.Security.Cryptography;

namespace RewardProgram.Application.Services.Auth;

public class OtpService : IOtpService
{
    private readonly IApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ILogger<OtpService> _logger;

    private const int OTP_LENGTH = 6;
    private const int OTP_EXPIRY_MINUTES = 5;
    private const int MAX_ATTEMPTS_PER_HOUR = 5;

    public OtpService(
        IApplicationDbContext context,
        ISmsService smsService,
        ILogger<OtpService> logger)
    {
        _context = context;
        _smsService = smsService;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateAndSendAsync(
        string mobileNumber,
        OtpPurpose purpose,
        string? registrationData = null)
    {
        // Check rate limiting
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentAttempts = await _context.OtpCodes
            .CountAsync(o => o.MobileNumber == mobileNumber &&
                            o.Purpose == purpose &&
                            o.CreatedAt >= oneHourAgo);

        if (recentAttempts >= MAX_ATTEMPTS_PER_HOUR)
        {
            return Result.Failure<string>(OtpErrors.TooManyAttempts);
        }

        // Invalidate previous unused OTPs
        var previousOtps = await _context.OtpCodes
            .Where(o => o.MobileNumber == mobileNumber &&
                       o.Purpose == purpose &&
                       !o.IsUsed)
            .ToListAsync();

        foreach (var previousOtp in previousOtps)
        {
            previousOtp.IsUsed = true;
        }

        // Generate new OTP
        var code = GenerateCode();

        var otpCode = new OtpCode
        {
            MobileNumber = mobileNumber,
            Code = code,
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES),
            IsUsed = false,
            RegistrationData = registrationData
        };

        await _context.OtpCodes.AddAsync(otpCode);
        await _context.SaveChangesAsync();

        // Send SMS
        var message = $"رمز التحقق الخاص بك هو: {code}";
        var sent = await _smsService.SendAsync(mobileNumber, message);

        if (!sent)
        {
            return Result.Failure<string>(OtpErrors.SendingFailed);
        }

        return Result.Success(code);
    }

    public async Task<Result<OtpCode>> VerifyAsync(string mobileNumber, string otp, OtpPurpose purpose)
    {
        var otpCode = await _context.OtpCodes
            .Where(o => o.MobileNumber == mobileNumber &&
                       o.Code == otp &&
                       o.Purpose == purpose &&
                       !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        return await ValidateOtpCode(otpCode);
    }

    private async Task<Result<OtpCode>> ValidateOtpCode(OtpCode? otpCode)
    {
        if (otpCode == null)
        {
            return Result.Failure<OtpCode>(OtpErrors.InvalidOtp);
        }

        if (otpCode.IsExpired)
        {
            return Result.Failure<OtpCode>(OtpErrors.ExpiredOtp);
        }

        if (otpCode.IsUsed)
        {
            return Result.Failure<OtpCode>(OtpErrors.AlreadyUsedOtp);
        }

        // Mark as used
        otpCode.IsUsed = true;
        await _context.SaveChangesAsync();

        return Result.Success(otpCode);
    }

    private static string GenerateCode()
    {
        // Use cryptographically secure random number generator
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }
}