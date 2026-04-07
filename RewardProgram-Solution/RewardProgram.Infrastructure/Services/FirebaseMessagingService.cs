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

        if (_enabled && FirebaseApp.DefaultInstance is null)
        {
            lock (_initLock)
            {
                if (FirebaseApp.DefaultInstance is null)
                {
                    var keyPath = options.Value.ServiceAccountKeyPath;
                    if (!string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
                    {
                        FirebaseApp.Create(new AppOptions
                        {
                            Credential = GoogleCredential.FromFile(keyPath)
                        });
                        _logger.LogInformation("Firebase initialized from {Path}", keyPath);
                    }
                    else
                    {
                        _enabled = false;
                        _logger.LogWarning("Firebase service account key not found at '{Path}'. FCM push disabled.", keyPath);
                    }
                }
            }
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
            _logger.LogDebug("FCM sent: {MessageId}", messageId);
            return true;
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        {
            _logger.LogInformation("FCM token expired/unregistered — token should be cleared");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM send failed");
            return true; // Don't clear token on transient errors
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
                _logger.LogDebug("FCM multicast: {Success}/{Total} succeeded",
                    response.SuccessCount, batch.Length);

                // Collect expired/unregistered tokens
                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var r = response.Responses[i];
                    if (!r.IsSuccess && r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    {
                        expiredTokens.Add(batch[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM multicast failed for batch of {Count}", batch.Length);
            }
        }

        return expiredTokens;
    }
}
