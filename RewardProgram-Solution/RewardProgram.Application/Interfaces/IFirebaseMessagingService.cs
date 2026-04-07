namespace RewardProgram.Application.Interfaces;

public interface IFirebaseMessagingService
{
    /// <summary>Returns false if the token is expired/unregistered and should be cleared.</summary>
    Task<bool> SendToUserAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null);

    /// <summary>Returns the list of expired/unregistered tokens that should be cleared.</summary>
    Task<IReadOnlyList<string>> SendToMultipleAsync(IReadOnlyList<string> fcmTokens, string title, string body, Dictionary<string, string>? data = null);
}
