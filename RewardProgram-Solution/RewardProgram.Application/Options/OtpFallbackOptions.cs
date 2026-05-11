namespace RewardProgram.Application.Options;

/// Configures the WhatsApp-to-SMS OTP fallback worker.
///
/// Flow: when /send-otp issues a WhatsApp Verify, the row is stamped with
/// FallbackEligibleAt = now + FallbackDelaySeconds. Every PollIntervalSeconds the
/// background worker scans for rows whose deadline has passed and that haven't
/// been verified yet, then issues a second Verify on the SMS channel for the
/// same number. The row's CurrentSid is rotated; PinId stays unchanged.
public class OtpFallbackOptions
{
    public const string SectionName = "OtpFallback";

    /// Master switch. When false, no rows are eligible for fallback and the
    /// worker is a no-op. Default off — turn on only in environments that hit
    /// real Twilio (UAT, Production).
    public bool Enabled { get; set; } = false;

    /// How long to wait for the user to enter the WhatsApp code before issuing
    /// a fallback SMS. WhatsApp typically delivers within a few seconds; this
    /// covers the slow-delivery / no-WhatsApp long tail.
    public int FallbackDelaySeconds { get; set; } = 25;

    /// How often the worker scans for due rows. Lower = quicker fallback, more
    /// DB load. 5s is a reasonable middle ground.
    public int PollIntervalSeconds { get; set; } = 5;

    /// Maximum rows processed per scan. Caps the burst when many users send-otp
    /// at the same moment.
    public int BatchSize { get; set; } = 20;
}
