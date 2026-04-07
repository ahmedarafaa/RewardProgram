namespace RewardProgram.Application.Contracts.Dashboard;

public record ShopOwnerDashboardResponse(
    string UserName,
    string? ProfileImageUrl,
    ShopOwnerShopInfo Shop,
    List<ShopOwnerSellerItem> Sellers,
    int TotalSellers,
    int TotalScans
);

public record ShopOwnerShopInfo(
    string CustomerCode,
    string StoreName,
    string VAT,
    string CRN,
    string ShopImageUrl,
    string ShortAddress,
    string District,
    string CityName,
    string Street,
    string PostalCode,
    int BuildingNumber,
    int SubNumber
);

public record ShopOwnerSellerItem(
    string Id,
    string Name,
    string MobileNumber,
    DateTime JoinedAt
);
