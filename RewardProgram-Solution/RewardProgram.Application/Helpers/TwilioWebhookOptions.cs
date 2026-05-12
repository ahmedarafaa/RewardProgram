namespace RewardProgram.Application.Helpers;

/// Settings for the Twilio Verify Event Streams webhook sink.
///
/// Twilio Verify v2 does NOT support per-request StatusCallback (that's Messages
/// API only). Verification delivery events are routed through Event Streams: a
/// Webhook sink in the Twilio Console POSTs CloudEvents JSON to our endpoint
/// whenever a Verify message hits a terminal state (delivered / failed). We act
/// on the "failed + whatsapp" combination to fire an SMS fallback.
///
/// Console setup (per environment):
///   1. Monitor → Event Streams → Sinks → Create Webhook sink pointing to
///      &lt;PublicBaseUrl&gt;/api/webhooks/twilio/verify-status
///   2. (Optional) set a Bearer token on the sink → mirror it in
///      EventStreamsBearerToken below for HMAC-style auth.
///   3. Create a Subscription on the sink for event type
///      com.twilio.verify.message_status.failed
public class TwilioWebhookOptions
{
    public const string SectionName = "TwilioWebhook";

    /// Master switch. When false the incoming webhook endpoint accepts POSTs but
    /// ignores them (returns 200) — keeps Twilio from hammering us with retries
    /// in environments where the sink is wired but auto-fallback isn't wanted.
    public bool Enabled { get; set; } = false;

    /// Public URL where this app is reachable from Twilio. Used for signature/
    /// URL reconstruction so reverse-proxied https:// is not mistaken for http://
    /// when Request.Scheme is stripped by IIS.
    public string? PublicBaseUrl { get; set; }

    /// Optional shared secret. When set, the controller requires
    /// `Authorization: Bearer &lt;EventStreamsBearerToken&gt;` on every webhook
    /// call. Configure this on the Twilio Webhook sink at creation, then mirror
    /// the value here. Leave null/empty to disable auth (only safe behind a
    /// non-public URL or IP allowlist).
    public string? EventStreamsBearerToken { get; set; }
}
