using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Notifications;

public record NotificationResponse(
    string Id,
    NotificationType Type,
    string Title,
    string Body,
    string? ReferenceId,
    bool IsRead,
    DateTime CreatedAt
);
