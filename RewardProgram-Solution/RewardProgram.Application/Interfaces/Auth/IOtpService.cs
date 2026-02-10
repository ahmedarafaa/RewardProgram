using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IOtpService
{
    Task<Result<string>> SendAsync(string mobileNumber, string? registrationData = null, CancellationToken ct = default);
    Task<Result<OtpVerificationResult>> VerifyAsync(string pinId, string otp, CancellationToken ct = default);
}
