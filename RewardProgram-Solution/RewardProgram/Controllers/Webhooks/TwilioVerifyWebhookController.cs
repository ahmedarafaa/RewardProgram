using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces.Auth;

namespace RewardProgram.API.Controllers.Webhooks;

/// Receives Twilio Event Streams CloudEvents for Verify message-status events.
/// When a WhatsApp delivery hits a terminal failure, fire an SMS fallback for
/// the same user — no client-side timer, no user-driven resend required.
///
/// Twilio configuration (per environment):
///   Monitor → Event Streams → Sinks → Webhook sink:
///     URL    https://<host>/api/webhooks/twilio/verify-status
///     Method POST
///   (Optional) Set a Bearer token on the sink and mirror it in
///   TwilioWebhook:EventStreamsBearerToken to authenticate incoming events.
///   Create a Subscription on the sink for event type
///   com.twilio.verify.message_status.failed (Verify → Message Status → Failed).
///
/// Payload shape — Twilio may send a single CloudEvent OR a JSON array of
/// CloudEvents (batched). We handle both.
[ApiController]
[Route("api/webhooks/twilio")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class TwilioVerifyWebhookController : ControllerBase
{
    private const string FailedEventTypeSuffix = ".failed";
    private const string CanceledEventTypeSuffix = ".canceled";
    // Twilio reports "Invalid Recipient" (63024) and similar non-WhatsApp-user
    // errors as Undelivered, not Failed. Both indicate the user never got the
    // OTP, so both should trigger the SMS fallback.
    private const string UndeliveredEventTypeSuffix = ".undelivered";

    private readonly IOtpFallbackService _fallbackService;
    private readonly TwilioWebhookOptions _webhookOptions;
    private readonly ILogger<TwilioVerifyWebhookController> _logger;

    public TwilioVerifyWebhookController(
        IOtpFallbackService fallbackService,
        IOptions<TwilioWebhookOptions> webhookOptions,
        ILogger<TwilioVerifyWebhookController> logger)
    {
        _fallbackService = fallbackService;
        _webhookOptions = webhookOptions.Value;
        _logger = logger;
    }

    [HttpPost("verify-status")]
    public async Task<IActionResult> VerifyStatus(CancellationToken ct)
    {
        // Disabled environments accept and ignore — keeps Twilio from retrying.
        if (!_webhookOptions.Enabled)
            return Ok();

        if (!ValidateBearerToken())
        {
            _logger.LogWarning("Twilio webhook: bearer token mismatch, rejecting");
            return Unauthorized();
        }

        string body;
        using (var reader = new StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("Twilio webhook: empty body, ignoring");
            return Ok();
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Twilio webhook: malformed JSON, ignoring");
            return Ok(); // 200 so Twilio doesn't retry malformed events forever
        }

        using (doc)
        {
            var events = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };

            foreach (var evt in events)
            {
                await HandleEventAsync(evt, ct);
            }
        }

        // Always 200: per-event errors are logged but never bubble up to Twilio,
        // which would retry the whole batch and amplify load.
        return Ok();
    }

    private async Task HandleEventAsync(JsonElement evt, CancellationToken ct)
    {
        // CloudEvents envelope: { specversion, type, source, id, time, data: {...} }
        // Action only on terminal failures of WhatsApp delivery.
        var eventType = TryGetString(evt, "type") ?? string.Empty;
        var isTerminalFailure =
            eventType.EndsWith(FailedEventTypeSuffix, StringComparison.OrdinalIgnoreCase) ||
            eventType.EndsWith(CanceledEventTypeSuffix, StringComparison.OrdinalIgnoreCase) ||
            eventType.EndsWith(UndeliveredEventTypeSuffix, StringComparison.OrdinalIgnoreCase);

        if (!isTerminalFailure)
            return; // delivered / read / pending → no-op

        if (!evt.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("Twilio webhook: event {Type} missing data object", eventType);
            return;
        }

        var channel = TryGetString(data, "channel");
        if (!string.Equals(channel, "whatsapp", StringComparison.OrdinalIgnoreCase))
            return; // only WhatsApp failures trigger SMS fallback

        // Twilio's Event Streams payload uses snake_case for verification_sid.
        // Accept the camelCase form too, just in case.
        var verificationSid = TryGetString(data, "verification_sid")
                           ?? TryGetString(data, "verificationSid");

        if (string.IsNullOrWhiteSpace(verificationSid))
        {
            _logger.LogWarning("Twilio webhook: {Type} event missing verification_sid", eventType);
            return;
        }

        var result = await _fallbackService.TriggerSmsForFailedVerificationAsync(verificationSid, ct);
        if (result.IsFailure)
        {
            _logger.LogError(
                "Twilio webhook: fallback service returned failure for Sid {Sid}: {Error}",
                verificationSid, result.Error.Code);
        }
    }

    private bool ValidateBearerToken()
    {
        // No token configured → auth disabled (acceptable when sink URL is secret
        // or behind an IP allowlist; Enabled flag still gates the endpoint).
        if (string.IsNullOrWhiteSpace(_webhookOptions.EventStreamsBearerToken))
            return true;

        var auth = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) ||
            !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = auth["Bearer ".Length..].Trim();
        // Constant-time comparison to avoid timing leaks on the secret.
        var expected = _webhookOptions.EventStreamsBearerToken;
        if (presented.Length != expected.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < expected.Length; i++)
            diff |= presented[i] ^ expected[i];
        return diff == 0;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }
}
