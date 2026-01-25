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

    /// <summary>
    /// Verifies OTP code for the specified mobile number and purpose.
    /// Always requires mobile number to prevent OTP hijacking attacks.
    /// </summary>
    Task<Result<OtpCode>> VerifyAsync(string mobileNumber, string otp, OtpPurpose purpose);
}