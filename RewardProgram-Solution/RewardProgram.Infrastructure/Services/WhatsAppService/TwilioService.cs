using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Infrastructure.Authentication;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Verify.V2.Service;
using Twilio.Types;

namespace RewardProgram.Infrastructure.Services.WhatsAppService;

public class TwilioService : ITwilioService
{
    private const string WebhookPath = "/api/webhooks/twilio/verify-status";

    private readonly TwilioOptions _options;
    private readonly TwilioWebhookOptions _webhookOptions;
    private readonly ILogger<TwilioService> _logger;
    private readonly bool _useMockMode;

    public TwilioService(
        IOptions<TwilioOptions> options,
        IOptions<TwilioWebhookOptions> webhookOptions,
        IHostEnvironment environment,
        ILogger<TwilioService> logger)
    {
        _options = options.Value;
        _webhookOptions = webhookOptions.Value;
        _logger = logger;

        // SECURITY: Mock mode is only allowed in non-production environments
        // (Development, Staging, UAT). If UseMockMode is set in any other
        // environment, fail startup rather than silently ignoring it — a
        // misconfigured flag otherwise lets "123456" bypass OTP verification
        // in Production.
        var mockAllowed = environment.IsDevelopment()
            || environment.IsStaging()
            || environment.IsEnvironment("UAT");

        if (_options.UseMockMode && !mockAllowed)
        {
            throw new InvalidOperationException(
                $"Twilio:UseMockMode is true but environment is '{environment.EnvironmentName}'. " +
                "Mock mode is only permitted in Development, Staging, or UAT. " +
                "Set UseMockMode=false for this environment.");
        }

        _useMockMode = _options.UseMockMode && mockAllowed;

        if (_useMockMode)
        {
            _logger.LogWarning("Twilio is running in MOCK MODE. OTP verification is bypassed!");
        }
        else
        {
            TwilioClient.Init(_options.AccountSid, _options.AuthToken);
        }

        // Twilio Verify v2 doesn't accept a per-request StatusCallback (Messages
        // API only). Delivery events are routed via Event Streams Webhook sinks
        // configured in the Twilio Console. Log the URL Twilio should POST to —
        // useful at startup to confirm the env's PublicBaseUrl matches what was
        // configured on the sink.
        if (_webhookOptions.Enabled && !string.IsNullOrWhiteSpace(_webhookOptions.PublicBaseUrl))
        {
            var url = _webhookOptions.PublicBaseUrl.TrimEnd('/') + WebhookPath;
            _logger.LogInformation("Twilio Event Streams webhook URL (configure on sink): {Uri}", url);
        }
    }

    public Task<Result<string>> SendOtpAsync(string mobileNumber, CancellationToken ct = default)
        => SendOtpAsync(mobileNumber, "whatsapp", ct);

    public async Task<Result<string>> SendOtpAsync(string mobileNumber, string channel, CancellationToken ct = default)
    {
        if (channel is not ("whatsapp" or "sms"))
        {
            return Result.Failure<string>(new Error(
                "Twilio.InvalidChannel",
                $"Unsupported Twilio channel: '{channel}'",
                500));
        }

        try
        {
            var formattedNumber = FormatMobileNumber(mobileNumber);

            if (_useMockMode)
            {
                var mockVerificationSid = $"VE{Guid.NewGuid():N}"[..34];

                _logger.LogInformation(
                    "[MOCK] OTP sent to {MobileNumber} via {Channel}, VerificationSid: {Sid}",
                    MobileNumberHelper.Mask(mobileNumber),
                    channel,
                    mockVerificationSid);

                return Result.Success(mockVerificationSid);
            }

            // Twilio Verify v2 doesn't accept a per-request statusCallback — that's
            // only on the Messages API. For Verify, delivery-status webhooks are
            // routed via Twilio Event Streams (out-of-band setup), which is heavier
            // than this feature warrants. We rely on:
            //   1. Sync-error retry in OtpService (catches most failure modes)
            //   2. User-initiated /resend-otp for the silent-fail edge case
            // The OtpFallbackService + webhook controller remain in place so an
            // Event Streams sink can be wired up later without further code changes.
            var verification = await VerificationResource.CreateAsync(
                to: formattedNumber,
                channel: channel,
                pathServiceSid: _options.VerifyServiceSid
            );

            if (verification.Status == "pending")
            {
                _logger.LogInformation(
                    "OTP sent successfully to {MobileNumber} via {Channel}, VerificationSid: {Sid}",
                    MobileNumberHelper.Mask(mobileNumber),
                    channel,
                    verification.Sid);

                return Result.Success(verification.Sid);
            }

            _logger.LogError("Unexpected Twilio verification status: {Status} for channel {Channel}",
                verification.Status, channel);
            return Result.Failure<string>(new Error(
                "Twilio.UnexpectedStatus",
                "فشل إرسال رمز التحقق",
                500));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending OTP to {MobileNumber} via {Channel}",
                MobileNumberHelper.Mask(mobileNumber), channel);

            return Result.Failure<string>(new Error(
                "Twilio.Exception",
                "فشل إرسال رمز التحقق",
                500));
        }
    }

    public async Task<Result> CancelVerificationAsync(string verificationSid, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(verificationSid))
            return Result.Success();

        if (_useMockMode)
        {
            _logger.LogInformation("[MOCK] Verification canceled, Sid: {Sid}", verificationSid);
            return Result.Success();
        }

        try
        {
            await VerificationResource.UpdateAsync(
                status: "canceled",
                pathServiceSid: _options.VerifyServiceSid,
                pathSid: verificationSid);

            _logger.LogInformation("Verification canceled, Sid: {Sid}", verificationSid);
            return Result.Success();
        }
        catch (Exception ex)
        {
            // Best-effort: a verification that's already approved / canceled /
            // expired returns 404 or 400 from Twilio. Either way, the dedup
            // window is gone and the next CreateAsync will get a fresh Sid.
            _logger.LogInformation(ex,
                "Cancel verification failed for Sid {Sid} — likely already in a final state. Continuing.",
                verificationSid);
            return Result.Success();
        }
    }

    public async Task<Result<bool>> VerifyOtpAsync(string verificationSid, string otp, CancellationToken ct = default)
    {
        try
        {
            if (_useMockMode)
            {
                var isValid = otp == "123456";

                _logger.LogInformation(
                    "[MOCK] OTP verification, Valid: {Valid}",
                    isValid);

                return Result.Success(isValid);
            }

            var verificationCheck = await VerificationCheckResource.CreateAsync(
                code: otp,
                verificationSid: verificationSid,
                pathServiceSid: _options.VerifyServiceSid
            );

            var isVerified = verificationCheck.Status == "approved";

            _logger.LogInformation(
                "OTP verification for Sid: {Sid}, Verified: {Verified}",
                verificationSid,
                isVerified);

            return Result.Success(isVerified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while verifying OTP for Sid: {Sid}", verificationSid);

            return Result.Failure<bool>(new Error(
                "Twilio.Exception",
                "فشل التحقق من الرمز",
                500));
        }
    }

    public async Task<Result> SendSmsAsync(string mobileNumber, string message, CancellationToken ct = default)
    {
        try
        {
            var formattedNumber = FormatMobileNumber(mobileNumber);

            if (_useMockMode)
            {
                _logger.LogInformation(
                    "[MOCK] SMS sent to {MobileNumber}: {Message}",
                    MobileNumberHelper.Mask(mobileNumber),
                    message);

                return Result.Success();
            }

            await MessageResource.CreateAsync(
                to: new PhoneNumber(formattedNumber),
                from: new PhoneNumber(_options.SmsFromNumber),
                body: message
            );

            _logger.LogInformation(
                "SMS sent to {MobileNumber}",
                MobileNumberHelper.Mask(mobileNumber));

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending SMS to {MobileNumber}",
                MobileNumberHelper.Mask(mobileNumber));

            return Result.Failure(new Error(
                "Twilio.SmsException",
                "فشل إرسال الرسالة النصية",
                500));
        }
    }

    public async Task<Result> SendWhatsAppMessageAsync(string mobileNumber, string contentSid, Dictionary<string, string>? contentVariables = null, CancellationToken ct = default)
    {
        try
        {
            var formattedNumber = FormatMobileNumber(mobileNumber);

            if (_useMockMode)
            {
                _logger.LogInformation(
                    "[MOCK] WhatsApp template message sent to {MobileNumber}, ContentSid: {ContentSid}",
                    MobileNumberHelper.Mask(mobileNumber),
                    contentSid);

                return Result.Success();
            }

            // Twilio MessageResource requires From and To to declare the same channel.
            // The To is prefixed with "whatsapp:" above, so From must be too — otherwise
            // Twilio rejects with "Invalid From and To pair. From and To should be of the
            // same channel".
            var messageOptions = new CreateMessageOptions(
                new PhoneNumber($"whatsapp:{formattedNumber}"))
            {
                From = new PhoneNumber($"whatsapp:{_options.WhatsAppFromNumber}"),
                ContentSid = contentSid
            };

            if (contentVariables != null)
            {
                messageOptions.ContentVariables = System.Text.Json.JsonSerializer.Serialize(contentVariables);
            }

            await MessageResource.CreateAsync(messageOptions);

            _logger.LogInformation(
                "WhatsApp template message sent to {MobileNumber}, ContentSid: {ContentSid}",
                MobileNumberHelper.Mask(mobileNumber),
                contentSid);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending WhatsApp message to {MobileNumber}",
                MobileNumberHelper.Mask(mobileNumber));

            return Result.Failure(new Error(
                "Twilio.WhatsAppException",
                "فشل إرسال رسالة الواتساب",
                500));
        }
    }

    #region Helpers

    private static string FormatMobileNumber(string mobileNumber)
    {
        if (mobileNumber.StartsWith("05"))
            return $"+966{mobileNumber[1..]}";

        if (mobileNumber.StartsWith("966"))
            return $"+{mobileNumber}";

        if (mobileNumber.StartsWith("+966"))
            return mobileNumber;

        // Egyptian numbers: 01XX → +20XX, 20XX → +20XX, +20XX → as-is
        if (mobileNumber.StartsWith("01") && mobileNumber.Length == 11)
            return $"+2{mobileNumber}";

        if (mobileNumber.StartsWith("20") && !mobileNumber.StartsWith("+"))
            return $"+{mobileNumber}";

        if (mobileNumber.StartsWith("+20"))
            return mobileNumber;

        throw new ArgumentException($"Unrecognized mobile number format: {mobileNumber[..Math.Min(4, mobileNumber.Length)]}***");
    }

    #endregion
}
