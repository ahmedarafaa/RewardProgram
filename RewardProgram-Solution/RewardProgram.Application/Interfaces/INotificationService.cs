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

    // User-facing
    Task<PaginatedResult<NotificationResponse>> GetUserNotificationsAsync(string userId, NotificationListQuery query, CancellationToken ct);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct);
    Task<Result> MarkAsReadAsync(string notificationId, string userId, CancellationToken ct);
    Task<Result> MarkAllAsReadAsync(string userId, CancellationToken ct);

    // Internal — called by other services
    Task CreateAsync(string userId, NotificationType type, string title, string body, string? referenceId = null, CancellationToken ct = default);

    // Admin
    Task<Result> SendToUserAsync(string targetUserId, string title, string body, string sentByAdminId, CancellationToken ct);
    Task<Result<int>> SendToRoleAsync(string roleName, string title, string body, string sentByAdminId, CancellationToken ct);
    Task<Result<int>> BroadcastAsync(string title, string body, string sentByAdminId, CancellationToken ct);
}
