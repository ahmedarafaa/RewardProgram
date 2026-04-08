using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record NotificationAnalyticsResponse(
    int TotalAllTime,
    int TotalThisMonth,
    int TotalToday,
    List<NotificationTypeCount> CountByType,
    int TotalRead,
    int TotalUnread,
    decimal ReadRate,
    int AdminSentCount,
    int SystemTriggeredCount,
    List<MonthlyCount> NotificationTrend
);

public record NotificationTypeCount(NotificationType Type, int Count);
