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
    private readonly IInfobipRepository _infobipRepository;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IInfobipRepository infobipRepository,
        IApplicationDbContext context,
        ILogger<OtpService> logger)
    {
        _infobipRepository = infobipRepository;
        _context = context;
        _logger = logger;
    }
    public async Task<Result<string>> SendAsync(string mobileNumber, string? registrationData = null)
    {
        var result = await _infobipRepository.SendOtpAsync(mobileNumber);

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
            IsUsed = false
        };

        await _context.OtpCodes.AddAsync(otpCode);
        await _context.SaveChangesAsync();

        _logger.LogInformation("OTP sent and stored for PinId: {PinId}, Mobile: {Mobile}",
            pinId, MaskMobileNumber(mobileNumber));

        return Result.Success(pinId);
    }

    public async Task<Result<string?>> VerifyAsync(string pinId, string otp)
    {
        // Find OTP record
        var otpCode = await _context.OtpCodes
            .FirstOrDefaultAsync(o => o.PinId == pinId && !o.IsUsed);

        if (otpCode == null)
        {
            _logger.LogWarning("OTP record not found for PinId: {PinId}", pinId);
            return Result.Failure<string?>(new Error(
                "Otp.NotFound",
                "رمز التحقق غير موجود",
                400));
        }

        // Verify with Infobip
        var result = await _infobipRepository.VerifyOtpAsync(pinId, otp);

        if (result.IsFailure)
        {
            return Result.Failure<string?>(result.Error);
        }

        if (!result.Value)
        {
            return Result.Failure<string?>(new Error(
                "Otp.InvalidOtp",
                "رمز التحقق غير صحيح",
                400));
        }

        // Mark as used
        otpCode.IsUsed = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("OTP verified successfully for PinId: {PinId}", pinId);

        return Result.Success(otpCode.RegistrationData);
    }

    private static string MaskMobileNumber(string mobileNumber)
    {
        if (string.IsNullOrEmpty(mobileNumber) || mobileNumber.Length < 4)
            return "****";

        return $"{mobileNumber[..3]}****{mobileNumber[^3..]}";
    }
}