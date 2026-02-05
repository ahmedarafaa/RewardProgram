using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Infrastructure.Authentication;
using RewardProgram.Infrastructure.InfobipDtos;
using System.Net.Http.Json;
using System.Text.Json;

namespace RewardProgram.Infrastructure.Services.WhatsAppService;

public class InfobipRepository : IInfobipRepository
{
    private readonly HttpClient _httpClient;
    private readonly InfobipOptions _options;
    private readonly ILogger<InfobipRepository> _logger;

    public InfobipRepository(
        HttpClient httpClient,
        IOptions<InfobipOptions> options,
        ILogger<InfobipRepository> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri($"https://{_options.BaseUrl}");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"App {_options.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }
    public async Task<Result<string>> SendOtpAsync(string mobileNumber)
    {
        try
        {
            var request = new SendOtpRequest(
                ApplicationId: _options.ApplicationId,
                MessageId: _options.MessageTemplateId,
                To: FormatMobileNumber(mobileNumber)
            );

            var response = await _httpClient.PostAsJsonAsync("/2fa/2/pin", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Infobip SendOtp failed. Status: {Status}, Response: {Response}",
                    response.StatusCode, errorContent);

                var errorResponse = JsonSerializer.Deserialize<InfobipErrorResponse>(errorContent);
                var errorMessage = errorResponse?.RequestError?.ServiceException?.Text
                    ?? "فشل إرسال رمز التحقق";

                return Result.Failure<string>(new Error(
                    "Infobip.SendFailed",
                    errorMessage,
                    (int)response.StatusCode));
            }

            var result = await response.Content.ReadFromJsonAsync<SendOtpResponse>();

            if (result == null || string.IsNullOrEmpty(result.PinId))
            {
                _logger.LogError("Infobip SendOtp returned null or empty PinId");
                return Result.Failure<string>(new Error(
                    "Infobip.InvalidResponse",
                    "فشل إرسال رمز التحقق",
                    500));
            }

            _logger.LogInformation("OTP sent successfully to {MobileNumber}, PinId: {PinId}",
                MaskMobileNumber(mobileNumber), result.PinId);

            return Result.Success(result.PinId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending OTP to {MobileNumber}",
                MaskMobileNumber(mobileNumber));

            return Result.Failure<string>(new Error(
                "Infobip.Exception",
                "فشل إرسال رمز التحقق",
                500));
        }
    }

    public async Task<Result<bool>> VerifyOtpAsync(string pinId, string otp)
    {
        try
        {
            var request = new VerifyOtpRequest(Pin: otp);

            var response = await _httpClient.PostAsJsonAsync($"/2fa/2/pin/{pinId}/verify", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Infobip VerifyOtp failed. Status: {Status}, Response: {Response}",
                    response.StatusCode, errorContent);

                var errorResponse = JsonSerializer.Deserialize<InfobipErrorResponse>(errorContent);
                var errorMessage = errorResponse?.RequestError?.ServiceException?.Text
                    ?? "رمز التحقق غير صحيح";

                return Result.Failure<bool>(new Error(
                    "Infobip.VerifyFailed",
                    errorMessage,
                    (int)response.StatusCode));
            }

            var result = await response.Content.ReadFromJsonAsync<VerifyOtpResponse>();

            if (result == null)
            {
                _logger.LogError("Infobip VerifyOtp returned null response");
                return Result.Failure<bool>(new Error(
                    "Infobip.InvalidResponse",
                    "فشل التحقق من الرمز",
                    500));
            }

            _logger.LogInformation("OTP verification for PinId: {PinId}, Verified: {Verified}",
                pinId, result.Verified);

            return Result.Success(result.Verified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while verifying OTP for PinId: {PinId}", pinId);

            return Result.Failure<bool>(new Error(
                "Infobip.Exception",
                "فشل التحقق من الرمز",
                500));
        }
    }

    #region Helpers
    private static string FormatMobileNumber(string mobileNumber)
    {
        // Convert 05XXXXXXXX to 9665XXXXXXXX
        if (mobileNumber.StartsWith("05"))
        {
            return $"966{mobileNumber[1..]}";
        }

        // Convert +9665XXXXXXXX to 9665XXXXXXXX
        if (mobileNumber.StartsWith("+966"))
        {
            return mobileNumber[1..];
        }

        // Convert 9665XXXXXXXX as-is
        if (mobileNumber.StartsWith("966"))
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
