namespace RewardProgram.Application.Contracts.Shop;

public record ShopMapItemResponse(
    string CustomerName,
    string ShopImageUrl,
    string CityName,
    string ShortAddress,
    string Street,
    string District,
    int BuildingNumber,
    string? Phone
);
