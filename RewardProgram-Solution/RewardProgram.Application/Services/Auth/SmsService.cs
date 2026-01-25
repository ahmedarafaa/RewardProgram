using Microsoft.Extensions.Logging;
using RewardProgram.Application.Interfaces.Auth;

namespace RewardProgram.Application.Services.Auth;

public class SmsService(ILogger<SmsService> logger) : ISmsService
{
    private readonly ILogger<SmsService> _logger = logger;


    public async Task<bool> SendAsync(string mobileNumber, string message)
    {
        // TODO: Integrate SMS provider (Infobip, Twilio, etc.)
        // For now, log the OTP for testing

        _logger.LogInformation(
            "SMS to {MobileNumber}: {Message}",
            mobileNumber,
            message);

        // Simulate async operation
        await Task.Delay(100);

        // Return true for testing
        return true;

        /* 
        // Infobip Example:
        
        var configuration = new Configuration()
        {
            BasePath = "https://api.infobip.com",
            ApiKey = _options.ApiKey
        };
        
        var sendSmsApi = new SendSmsApi(configuration);
        
        var smsMessage = new SmsTextualMessage()
        {
            From = _options.SenderName,
            Destinations = new List<SmsDestination>()
            {
                new SmsDestination(to: mobileNumber)
            },
            Text = message
        };
        
        var smsRequest = new SmsAdvancedTextualRequest()
        {
            Messages = new List<SmsTextualMessage>() { smsMessage }
        };
        
        try
        {
            var smsResponse = await sendSmsApi.SendSmsMessageAsync(smsRequest);
            return smsResponse.Messages.First().Status.GroupName == "PENDING";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {MobileNumber}", mobileNumber);
            return false;
        }
        */
    }
}
