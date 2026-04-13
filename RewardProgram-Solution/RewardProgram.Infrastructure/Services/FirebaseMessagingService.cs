using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Infrastructure.Services;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";
    public string ServiceAccountKeyPath { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public class FirebaseMessagingService : IFirebaseMessagingService
{
    private static readonly object _initLock = new();
    private readonly ILogger<FirebaseMessagingService> _logger;
    private readonly bool _enabled;

    public FirebaseMessagingService(IOptions<FirebaseOptions> options, ILogger<FirebaseMessagingService> logger)
    {
        _logger = logger;
        _enabled = options.Value.Enabled;

        if (!_enabled)
        {
            _logger.LogInformation("Firebase push notifications are disabled via configuration.");
            return;
        }

        if (FirebaseApp.DefaultInstance is not null)
            return;

        lock (_initLock)
        {
            if (FirebaseApp.DefaultInstance is not null)
                return;

            var keyPath = options.Value.ServiceAccountKeyPath;
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(keyPath)
            });
            _logger.LogInformation("Firebase initialized from {Path}", keyPath);
        }
    }

    public async Task<bool> SendToUserAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null)
    {
        if (!_enabled) return true;

        var message = new Message
        {
            Token = fcmToken,
            Notification = new Notification { Title = title, Body = body },
            Data = data
        };

        try
        {
            var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("FCM send ok MessageId={MessageId}", messageId);
            return true;
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        {
            _logger.LogInformation("FCM token unregistered — clearing token");
            return false;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogWarning(ex, "FCM send failed Code={Code}", ex.MessagingErrorCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM send failed (transient)");
            return true;
        }
    }

    public async Task<IReadOnlyList<string>> SendToMultipleAsync(IReadOnlyList<string> fcmTokens, string title, string body, Dictionary<string, string>? data = null)
    {
        if (!_enabled || fcmTokens.Count == 0) return [];

        var expiredTokens = new List<string>();

        foreach (var batch in fcmTokens.Chunk(500))
        {
            var message = new MulticastMessage
            {
                Tokens = batch.ToList(),
                Notification = new Notification { Title = title, Body = body },
                Data = data
            };

            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);

                var batchExpired = 0;
                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var r = response.Responses[i];
                    if (!r.IsSuccess && r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    {
                        expiredTokens.Add(batch[i]);
                        batchExpired++;
                    }
                }

                _logger.LogInformation(
                    "FCM multicast Success={Success} Failure={Failure} Expired={Expired} Total={Total}",
                    response.SuccessCount, response.FailureCount, batchExpired, batch.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM multicast failed for batch of {Count}", batch.Length);
            }
        }

        return expiredTokens;
    }
}
