using RewardProgram.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IInfobipRepository
{
    Task<Result<string>> SendOtpAsync(string mobileNumber);
    Task<Result<bool>> VerifyOtpAsync(string pinId, string otp);
}