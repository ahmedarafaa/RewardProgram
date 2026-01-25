using RewardProgram.Application.Abstractions;
using RewardProgram.Domain.Entities.OTP;
using RewardProgram.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IOtpService
{
    Task<Result<string>> GenerateAndSendAsync(string mobileNumber, OtpPurpose purpose, string? registrationData = null);
    Task<Result<OtpCode>> VerifyAsync(string otp, OtpPurpose purpose);
    Task<Result<OtpCode>> VerifyAsync(string mobileNumber, string otp, OtpPurpose purpose);
}