namespace RewardProgram.Application.Contracts.Auth;

/// <summary>
/// Shop data echoed back on shop-owner / seller registration so the client
/// has the persisted values (incl. the server-stored image URL) without an
/// extra round-trip. Null for user types that have no shop data (technician).
/// </summary>
public record RegisteredShopDataResponse(
    string CustomerCode,
    string StoreName,
    string VAT,
    string CRN,
    string ShortAddress,
    string District,
    string Street,
    int BuildingNumber,
    string PostalCode,
    int SubNumber,
    string CityId,
    string ShopImageUrl
);
