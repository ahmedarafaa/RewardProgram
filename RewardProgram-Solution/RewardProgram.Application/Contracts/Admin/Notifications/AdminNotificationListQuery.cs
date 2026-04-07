using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Notifications;

public record AdminNotificationListQuery(
    string? TargetUserId = null,
    NotificationType? Type = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public record AdminNotificationHistoryItem(
    string Id,
    string UserId,
    string UserName,
    NotificationType Type,
    string Title,
    string Body,
    string? ReferenceId,
    bool IsRead,
    DateTime CreatedAt
);
