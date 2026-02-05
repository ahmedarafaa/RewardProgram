using RewardProgram.Application.Abstractions;
using RewardProgram.Domain.Entities.OTP;
using RewardProgram.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IOtpService
{
    Task<Result<string>> SendAsync(string mobileNumber, string? registrationData = null);
    Task<Result<string?>> VerifyAsync(string pinId, string otp);
}