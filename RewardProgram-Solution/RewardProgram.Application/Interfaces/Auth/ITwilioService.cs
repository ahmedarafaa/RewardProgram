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

    // Cancels a pending Twilio Verify verification. Used by /resend-otp to
    // kill the (Service, To) dedup window before issuing a fresh send on a
    // different channel — otherwise Twilio returns the existing pending Sid
    // without actually delivering anything new. Best-effort: returns Success
    // even if Twilio rejects the cancel (already approved / canceled / expired).
    Task<Result> CancelVerificationAsync(string verificationSid, CancellationToken ct = default);
    Task<Result> SendSmsAsync(string mobileNumber, string message, CancellationToken ct = default);
    Task<Result> SendWhatsAppMessageAsync(string mobileNumber, string contentSid, Dictionary<string, string>? contentVariables = null, CancellationToken ct = default);
}
