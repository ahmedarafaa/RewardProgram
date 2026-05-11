using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Options;
using RewardProgram.Infrastructure.Authentication;
using Twilio.Security;

namespace RewardProgram.API.Controllers.Webhooks;

/// Receives Verify status callbacks from Twilio. When a WhatsApp delivery moves
/// to a terminal failure (failed / canceled), trigger an SMS fallback for the
/// same user — no client-side timer required.
///
/// Twilio configuration: in the Verify Service settings, set
///   Status Callback URL → https://&lt;host&gt;/api/webhooks/twilio/verify-status
///   Method               → HTTP POST
/// Twilio signs each POST with X-Twilio-Signature (HMAC-SHA1 of URL+form params
/// using the account AuthToken). We validate that signature on every request.
[ApiController]
[Route("api/webhooks/twilio")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class TwilioVerifyWebhookController : ControllerBase
{
    private readonly IOtpFallbackService _fallbackService;
    private readonly TwilioOptions _twilioOptions;
    private readonly TwilioWebhookOptions _webhookOptions;
    private readonly ILogger<TwilioVerifyWebhookController> _logger;

    public TwilioVerifyWebhookController(
        IOtpFallbackService fallbackService,
        IOptions<TwilioOptions> twilioOptions,
        IOptions<TwilioWebhookOptions> webhookOptions,
        ILogger<TwilioVerifyWebhookController> logger)
    {
        _fallbackService = fallbackService;
        _twilioOptions = twilioOptions.Value;
        _webhookOptions = webhookOptions.Value;
        _logger = logger;
    }

    [HttpPost("verify-status")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> VerifyStatus(CancellationToken ct)
    {
        // Disabled environments accept and ignore — keeps Twilio from retrying.
        if (!_webhookOptions.Enabled)
            return Ok();

        if (!Request.HasFormContentType)
        {
            _logger.LogWarning("Twilio webhook: rejected — not form content");
            return BadRequest();
        }

        var form = await Request.ReadFormAsync(ct);

        if (!ValidateTwilioSignature(form))
        {
            _logger.LogWarning("Twilio webhook: invalid signature, rejecting");
            return Unauthorized();
        }

        var verificationSid = form["VerificationSid"].ToString();
        var status = form["Status"].ToString();
        var channel = form["Channel"].ToString();

        // Only act on terminal WhatsApp failures. Approved/pending hits are no-ops.
        var isFailure = string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase);
        var isWhatsApp = string.Equals(channel, "whatsapp", StringComparison.OrdinalIgnoreCase);

        if (!isFailure || !isWhatsApp)
            return Ok();

        if (string.IsNullOrWhiteSpace(verificationSid))
        {
            _logger.LogWarning("Twilio webhook: failure event missing VerificationSid");
            return Ok(); // 200 so Twilio doesn't keep retrying a malformed event
        }

        // Service handles all branching (row not found, already used, etc.).
        // We always return 200 — Twilio retries indefinitely on 5xx, and the
        // result of the fallback is internal to us.
        var result = await _fallbackService.TriggerSmsForFailedVerificationAsync(verificationSid, ct);
        if (result.IsFailure)
        {
            _logger.LogError(
                "Twilio webhook: fallback service returned failure for Sid {Sid}: {Error}",
                verificationSid, result.Error.Code);
        }

        return Ok();
    }

    private bool ValidateTwilioSignature(IFormCollection form)
    {
        var signatureHeader = Request.Headers["X-Twilio-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        if (string.IsNullOrWhiteSpace(_twilioOptions.AuthToken))
        {
            _logger.LogError("Twilio webhook: AuthToken not configured, cannot validate signature");
            return false;
        }

        // Reconstruct the URL Twilio signed. Behind a reverse proxy (IIS / SmarterASP)
        // the incoming Request.Scheme can be http even when the public URL is https,
        // so PublicBaseUrl can be set as an override.
        string url;
        if (!string.IsNullOrWhiteSpace(_webhookOptions.PublicBaseUrl))
        {
            url = _webhookOptions.PublicBaseUrl.TrimEnd('/') + Request.Path + Request.QueryString;
        }
        else
        {
            url = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        }

        var parameters = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var validator = new RequestValidator(_twilioOptions.AuthToken);
        return validator.Validate(url, parameters, signatureHeader);
    }
}
