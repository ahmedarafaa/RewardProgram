namespace RewardProgram.Application.Helpers;

/// Settings for the Twilio Verify status webhook.
///
/// TwilioService passes PublicBaseUrl + "/api/webhooks/twilio/verify-status" as
/// the `StatusCallback` parameter on every VerificationResource.CreateAsync call.
/// Twilio then POSTs to that URL whenever the Verification's status changes
/// (delivered, failed, canceled, etc.). We use the "failed" / "canceled" signal
/// to trigger an SMS fallback for the same number — no client-side timer.
///
/// Per-request StatusCallback avoids the Twilio Console UI entirely — the URL is
/// versioned in this options class, not hidden in a console setting that can
/// drift between environments. The AuthToken (Twilio:AuthToken) is still used
/// to validate the X-Twilio-Signature header on incoming webhook calls.
public class TwilioWebhookOptions
{
    public const string SectionName = "TwilioWebhook";

    /// Master switch. When false:
    ///   - StatusCallback is NOT passed to Twilio (no webhook will ever fire)
    ///   - The incoming webhook endpoint accepts POSTs but ignores them (returns 200)
    /// Use this in environments that don't talk to real Twilio (Development /
    /// Staging mock mode) so misconfigured callbacks are noise-free.
    public bool Enabled { get; set; } = false;

    /// Public URL where this app is reachable from Twilio. Used for two purposes:
    ///   1. Composing the StatusCallback URL passed to Twilio on each Verify create.
    ///   2. Signature-validation URL reconstruction (so reverse-proxied https:// is
    ///      not mistaken for http:// when Request.Scheme is stripped by IIS).
    /// Required when Enabled=true.
    public string? PublicBaseUrl { get; set; }
}
