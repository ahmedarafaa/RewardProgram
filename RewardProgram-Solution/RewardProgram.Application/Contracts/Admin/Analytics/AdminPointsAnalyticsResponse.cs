namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record AdminPointsAnalyticsResponse(
    decimal TotalEarned,
    decimal TotalRedeemed,
    decimal TotalBalance,
    List<RegionPointsItem> PointsByRegion,
    List<RepresentativePointsItem> PointsByRepresentative,
    List<MonthlyDecimalCount> PointsTrend
);

public record RegionPointsItem(string RegionId, string RegionNameAr, string RegionNameEn, decimal TotalEarned);
public record RepresentativePointsItem(string SalesManId, string SalesManName, decimal TotalEarned, int UserCount);
public record MonthlyDecimalCount(int Year, int Month, decimal Total);
