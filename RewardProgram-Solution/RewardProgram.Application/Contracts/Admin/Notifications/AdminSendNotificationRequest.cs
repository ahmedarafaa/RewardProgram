namespace RewardProgram.Application.Contracts.Admin.Notifications;

public record AdminSendNotificationRequest(
    string? TargetUserId,
    string? RoleName,
    string Title,
    string Body
);
