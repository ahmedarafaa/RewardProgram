namespace RewardProgram.Application.Interfaces;

public interface IFirebaseMessagingService
{
    Task SendToUserAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null);
    Task SendToMultipleAsync(IReadOnlyList<string> fcmTokens, string title, string body, Dictionary<string, string>? data = null);
}
