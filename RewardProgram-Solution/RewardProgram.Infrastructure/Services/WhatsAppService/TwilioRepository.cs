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

public class TwilioRepository : ITwilioRepository
{
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioRepository> _logger;
    private readonly bool _useMockMode;

    public TwilioRepository(
        IOptions<TwilioOptions> options,
        IHostEnvironment environment,
        ILogger<TwilioRepository> logger)
    {
        _options = options.Value;
        _logger = logger;

        // SECURITY: Mock mode is ONLY allowed in Development environment
        _useMockMode = _options.UseMockMode && environment.IsDevelopment();

        if (_options.UseMockMode && !environment.IsDevelopment())
        {
            _logger.LogWarning(
                "Twilio UseMockMode is enabled in configuration but IGNORED because environment is {Environment}. " +
                "Mock mode is only allowed in Development.",
                environment.EnvironmentName);
        }

        if (_useMockMode)
        {
            _logger.LogWarning("Twilio is running in MOCK MODE. OTP verification is bypassed!");
        }
    }

    public async Task<Result<string>> SendOtpAsync(string mobileNumber)
    {
        try
        {
            var formattedNumber = FormatMobileNumber(mobileNumber);

            if (_useMockMode)
            {
                var mockVerificationSid = $"VE{Guid.NewGuid():N}"[..34];

                _logger.LogInformation(
                    "[MOCK] OTP sent to {MobileNumber}, VerificationSid: {Sid}",
                    MobileNumberHelper.Mask(mobileNumber),
                    mockVerificationSid);

                return Result.Success(mockVerificationSid);
            }

            TwilioClient.Init(_options.AccountSid, _options.AuthToken);

            var verification = await VerificationResource.CreateAsync(
                to: formattedNumber,
                channel: "whatsapp",
                pathServiceSid: _options.VerifyServiceSid
            );

            if (verification.Status == "pending")
            {
                _logger.LogInformation(
                    "OTP sent successfully to {MobileNumber}, VerificationSid: {Sid}",
                    MobileNumberHelper.Mask(mobileNumber),
                    verification.Sid);

                return Result.Success(verification.Sid);
            }

            _logger.LogError("Unexpected Twilio verification status: {Status}", verification.Status);
            return Result.Failure<string>(new Error(
                "Twilio.UnexpectedStatus",
                "فشل إرسال رمز التحقق",
                500));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending OTP to {MobileNumber}",
                MobileNumberHelper.Mask(mobileNumber));

            return Result.Failure<string>(new Error(
                "Twilio.Exception",
                "فشل إرسال رمز التحقق",
                500));
        }
    }

    public async Task<Result<bool>> VerifyOtpAsync(string verificationSid, string otp)
    {
        try
        {
            if (_useMockMode)
            {
                var isValid = otp.Length == 6 && otp.All(char.IsDigit);

                _logger.LogInformation(
                    "[MOCK] OTP verification for Sid: {Sid}, Valid: {Valid}",
                    verificationSid,
                    isValid);

                return Result.Success(isValid);
            }

            TwilioClient.Init(_options.AccountSid, _options.AuthToken);

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

    public async Task<Result> SendWhatsAppMessageAsync(string mobileNumber, string message)
    {
        try
        {
            var formattedNumber = FormatMobileNumber(mobileNumber);

            if (_useMockMode)
            {
                _logger.LogInformation(
                    "[MOCK] WhatsApp message sent to {MobileNumber}: {Message}",
                    MobileNumberHelper.Mask(mobileNumber),
                    message);

                return Result.Success();
            }

            TwilioClient.Init(_options.AccountSid, _options.AuthToken);

            await MessageResource.CreateAsync(
                to: new PhoneNumber($"whatsapp:{formattedNumber}"),
                from: new PhoneNumber(_options.WhatsAppFromNumber),
                body: message
            );

            _logger.LogInformation(
                "WhatsApp message sent to {MobileNumber}",
                MobileNumberHelper.Mask(mobileNumber));

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

        return mobileNumber;
    }

    #endregion
}
