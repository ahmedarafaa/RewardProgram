using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Interfaces.Auth;

public interface ITwilioService
{
    Task<Result<string>> SendOtpAsync(string mobileNumber, CancellationToken ct = default);
    Task<Result<bool>> VerifyOtpAsync(string verificationSid, string otp, CancellationToken ct = default);
    Task<Result> SendWhatsAppMessageAsync(string mobileNumber, string message, CancellationToken ct = default);
}
