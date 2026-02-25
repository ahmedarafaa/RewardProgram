using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Auth;

internal record SellerRegistrationData(
    UserType UserType,
    string Name,
    string MobileNumber,
    string CustomerCode,
    string AssignedSalesManId,
    string CityId,
    bool ShopDataAlreadyExists,
    // Shop data fields — only used when ShopDataAlreadyExists == false
    string? StoreName,
    string? VAT,
    string? CRN,
    string? ShopImageUrl,
    string? Street,
    int? BuildingNumber,
    string? PostalCode,
    int? SubNumber
);
