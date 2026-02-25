using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Auth;

internal record ShopOwnerRegistrationData(
    UserType UserType,
    string CustomerCode,
    string OwnerName,
    string MobileNumber,
    string CityId,
    string AssignedSalesManId,
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
