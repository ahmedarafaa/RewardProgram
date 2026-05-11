namespace RewardProgram.Application.Helpers;

/// Settings for the Twilio Verify status webhook.
///
/// Twilio Verify POSTs to {WebhookBaseUrl}/api/webhooks/twilio/verify-status
/// whenever a Verification's status changes (delivered, failed, canceled, etc.).
/// We use the "failed" / "canceled" signal to trigger an SMS fallback for the
/// same number — no client-side timer required.
///
/// Configure in Twilio console:
///   Verify Service → Status Callback URL → https://&lt;your-host&gt;/api/webhooks/twilio/verify-status
/// And keep Twilio:AuthToken in sync — the webhook validates the X-Twilio-Signature
/// header against that auth token.
public class TwilioWebhookOptions
{
    public const string SectionName = "TwilioWebhook";

    /// Master switch. When false, the webhook endpoint accepts POSTs but ignores
    /// them (returns 200). Use this in environments that don't talk to real Twilio
    /// (Development / Staging mock mode) so misconfigured callbacks are noise-free.
    public bool Enabled { get; set; } = false;

    /// Public URL where this app is reachable from Twilio. Used only for signature
    /// validation reconstruction when the app sits behind a proxy that rewrites
    /// Host/Scheme. Leave empty to use the incoming request's URL.
    public string? PublicBaseUrl { get; set; }
}
