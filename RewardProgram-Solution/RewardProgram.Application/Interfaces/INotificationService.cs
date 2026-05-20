using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Notifications;
using RewardProgram.Application.Contracts.Notifications;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Interfaces;

public interface INotificationService
{
    // Device registration
    Task<Result> RegisterDeviceAsync(string userId, string fcmToken, CancellationToken ct);
    Task<Result> UnregisterDeviceAsync(string userId, CancellationToken ct);

    // User-facing
    Task<PaginatedResult<NotificationResponse>> GetUserNotificationsAsync(string userId, NotificationListQuery query, CancellationToken ct);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct);
    Task<Result> MarkAsReadAsync(string notificationId, string userId, CancellationToken ct);
    Task<Result> MarkAllAsReadAsync(string userId, CancellationToken ct);
    Task<Result> DeleteNotificationAsync(string notificationId, string userId, CancellationToken ct);

    // Notification preferences
    Task<List<NotificationPreferenceItem>> GetPreferencesAsync(string userId, CancellationToken ct);
    Task<Result> UpdatePreferencesAsync(string userId, UpdateNotificationPreferencesRequest request, CancellationToken ct);

    // Internal — called by other services. title/body are the legacy (Arabic) text that
    // older clients fall back to; titleKey/bodyKey are the Tranche-3 resource keys that
    // the read path uses to render in the requesting user's locale. bodyArgs is a JSON
    // array of strings substituted into the body template's {0},{1}... placeholders.
    Task CreateAsync(string userId, NotificationType type, string title, string body,
        string? referenceId = null,
        string? titleKey = null,
        string? bodyKey = null,
        string? bodyArgs = null,
        CancellationToken ct = default);

    // Admin
    Task<Result> SendToUserAsync(string targetUserId, string title, string body, string sentByAdminId, CancellationToken ct);
    Task<Result<int>> SendToRoleAsync(string roleName, string title, string body, string sentByAdminId, CancellationToken ct);
    Task<Result<int>> BroadcastAsync(string title, string body, string sentByAdminId, CancellationToken ct);
    Task<PaginatedResult<AdminNotificationHistoryItem>> GetNotificationHistoryAsync(AdminNotificationListQuery query, CancellationToken ct);
    Task<Result<List<AdminNotificationHistoryItem>>> ExportNotificationHistoryAsync(AdminNotificationListQuery query, CancellationToken ct);
}
