namespace RewardProgram.Application.Contracts.Admin.Analytics;

public record AdminRegionAnalyticsResponse(
    List<RegionAnalyticsItem> Regions
);

public record RegionAnalyticsItem(
    string RegionId,
    string RegionNameAr,
    string RegionNameEn,
    string? ZoneManagerName,
    int CityCount,
    int ShopOwnerCount,
    int SellerCount,
    int TechnicianCount,
    List<CityAnalyticsItem> Cities
);

public record CityAnalyticsItem(
    string CityId,
    string CityNameAr,
    string CityNameEn,
    string? ApprovalSalesManName,
    int UserCount
);
