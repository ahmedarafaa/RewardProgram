using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Interfaces.Auth;

/// Triggers an SMS fallback for an OTP whose WhatsApp delivery failed
/// asynchronously. Invoked by the Twilio status webhook when Verify reports
/// a "failed" or "canceled" event for a WhatsApp-channel verification.
public interface IOtpFallbackService
{
    /// Looks up the OtpCode by Twilio's VerificationSid (== CurrentSid), and if
    /// the row is still pending and hasn't already fallen back, issues an SMS
    /// Verify and rotates CurrentSid to the new Sid.
    ///
    /// Returns Success(false) when no action was taken (row not found, already
    /// fired, already used, or expired) — these are normal idempotent cases.
    /// Returns Success(true) when SMS was actually sent.
    Task<Result<bool>> TriggerSmsForFailedVerificationAsync(
        string verificationSid,
        CancellationToken ct = default);
}
