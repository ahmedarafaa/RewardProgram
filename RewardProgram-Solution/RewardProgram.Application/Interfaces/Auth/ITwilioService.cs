using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Interfaces.Auth;

public interface ITwilioService
{
    // Defaults to the WhatsApp channel — kept as the existing entry-point so
    // current callers stay unchanged.
    Task<Result<string>> SendOtpAsync(string mobileNumber, CancellationToken ct = default);

    // Explicit-channel overload. Channel must be "whatsapp" or "sms".
    // Used by the SMS fallback worker after the WhatsApp window elapses.
    Task<Result<string>> SendOtpAsync(string mobileNumber, string channel, CancellationToken ct = default);

    Task<Result<bool>> VerifyOtpAsync(string verificationSid, string otp, CancellationToken ct = default);
    Task<Result> SendSmsAsync(string mobileNumber, string message, CancellationToken ct = default);
    Task<Result> SendWhatsAppMessageAsync(string mobileNumber, string contentSid, Dictionary<string, string>? contentVariables = null, CancellationToken ct = default);
}
