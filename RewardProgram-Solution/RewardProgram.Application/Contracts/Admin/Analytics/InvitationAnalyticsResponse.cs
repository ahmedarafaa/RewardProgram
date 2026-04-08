namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record InvitationAnalyticsResponse(
    int TotalInvitationsSent,
    int TotalAccepted,
    int TotalPending,
    decimal ConversionRate,
    List<TopInviterItem> TopInviters,
    decimal TotalRewardPointsSpent,
    decimal TotalRewardSarSpent,
    List<MonthlyCount> InvitationTrend
);

public record TopInviterItem(
    string UserId,
    string UserName,
    string MobileNumber,
    int TotalInvited,
    int ApprovedCount,
    decimal PointsEarned
);
