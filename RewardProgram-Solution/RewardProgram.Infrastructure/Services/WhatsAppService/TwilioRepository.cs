using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Infrastructure.Authentication;

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
        // Even if UseMockMode is true in config, it will be disabled in Production
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
                // DEVELOPMENT ONLY: Return mock verification SID
                var mockVerificationSid = $"VE{Guid.NewGuid():N}"[..34];

                _logger.LogInformation(
                    "[MOCK] OTP sent to {MobileNumber}, VerificationSid: {Sid}",
                    MaskMobileNumber(mobileNumber),
                    mockVerificationSid);

                return Result.Success(mockVerificationSid);
            }

            // Production: Use real Twilio API
            // Uncomment and configure when Twilio credentials are set up:
            /*
            TwilioClient.Init(_options.AccountSid, _options.AuthToken);

            var verification = await VerificationResource.CreateAsync(
                to: formattedNumber,
                channel: "whatsapp", // or "sms"
                pathServiceSid: _options.VerifyServiceSid
            );

            if (verification.Status == "pending")
            {
                _logger.LogInformation(
                    "OTP sent successfully to {MobileNumber}, VerificationSid: {Sid}",
                    MaskMobileNumber(mobileNumber),
                    verification.Sid);

                return Result.Success(verification.Sid);
            }
            */

            // Fallback error if Twilio is not configured
            _logger.LogError("Twilio is not configured. Set UseMockMode=true for development or configure Twilio credentials.");
            return Result.Failure<string>(new Error(
                "Twilio.NotConfigured",
                "خدمة الرسائل غير مفعلة",
                500));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending OTP to {MobileNumber}",
                MaskMobileNumber(mobileNumber));

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
                // DEVELOPMENT ONLY: Accept any 6-digit OTP
                var isValid = otp.Length == 6 && otp.All(char.IsDigit);

                _logger.LogInformation(
                    "[MOCK] OTP verification for Sid: {Sid}, Valid: {Valid}",
                    verificationSid,
                    isValid);

                return Result.Success(isValid);
            }

            // Production: Use real Twilio API
            // Uncomment and configure when Twilio credentials are set up:
            /*
            TwilioClient.Init(_options.AccountSid, _options.AuthToken);

            // Note: Twilio Verify uses phone number for check, not Sid
            // We need to store the phone number with the Sid
            var verificationCheck = await VerificationCheckResource.CreateAsync(
                to: phoneNumber,
                code: otp,
                pathServiceSid: _options.VerifyServiceSid
            );

            var isVerified = verificationCheck.Status == "approved";

            _logger.LogInformation(
                "OTP verification for {PhoneNumber}, Verified: {Verified}",
                MaskMobileNumber(phoneNumber),
                isVerified);

            return Result.Success(isVerified);
            */

            // Fallback error if Twilio is not configured
            _logger.LogError("Twilio is not configured. Set UseMockMode=true for development or configure Twilio credentials.");
            return Result.Failure<bool>(new Error(
                "Twilio.NotConfigured",
                "خدمة الرسائل غير مفعلة",
                500));
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

    #region Helpers
    private static string FormatMobileNumber(string mobileNumber)
    {
        // Convert 05XXXXXXXX to +9665XXXXXXXX
        if (mobileNumber.StartsWith("05"))
        {
            return $"+966{mobileNumber[1..]}";
        }

        // Convert 9665XXXXXXXX to +9665XXXXXXXX
        if (mobileNumber.StartsWith("966"))
        {
            return $"+{mobileNumber}";
        }

        // Already formatted
        if (mobileNumber.StartsWith("+966"))
        {
            return mobileNumber;
        }

        return mobileNumber;
    }

    private static string MaskMobileNumber(string mobileNumber)
    {
        if (string.IsNullOrEmpty(mobileNumber) || mobileNumber.Length < 4)
            return "****";

        return $"{mobileNumber[..3]}****{mobileNumber[^3..]}";
    } 
    #endregion
}