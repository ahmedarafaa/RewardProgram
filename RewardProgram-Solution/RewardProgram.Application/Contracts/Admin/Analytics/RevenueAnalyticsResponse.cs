namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record RevenueAnalyticsResponse(
    decimal TotalSarLiability,
    decimal TotalSarHeld,
    decimal TotalSarPaidOut,
    decimal TotalPointsOutstanding,
    List<TransactionTypeVolume> VolumeByType,
    List<MonthlyDecimalCount> PayoutTrend
);

public record TransactionTypeVolume(string Type, int Count, decimal TotalPoints, decimal TotalSar);
