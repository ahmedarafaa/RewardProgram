using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record RedemptionAnalyticsResponse(
    List<RedemptionStatusCount> CountByStatus,
    List<RedemptionMethodCount> CountByMethod,
    decimal TotalSarRedeemed,
    decimal AverageProcessingDays,
    int PendingCount,
    List<MonthlyDecimalCount> RedemptionTrend
);

public record RedemptionStatusCount(RedemptionRequestStatus Status, int Count, decimal TotalSar);
public record RedemptionMethodCount(RedemptionMethod Method, int Count, decimal TotalSar);
