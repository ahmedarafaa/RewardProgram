namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record SalesManPerformanceResponse(
    List<SalesManPerformanceItem> SalesMen
);

public record SalesManPerformanceItem(
    string SalesManId,
    string SalesManName,
    string MobileNumber,
    int AssignedUserCount,
    int ApprovedUserCount,
    int PendingApprovalCount,
    decimal TotalPointsEarned,
    int CityCount
);
