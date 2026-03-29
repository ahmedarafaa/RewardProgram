namespace RewardProgram.Application.Contracts.Notifications;

public record NotificationListQuery(
    int Page = 1,
    int PageSize = 20
);
