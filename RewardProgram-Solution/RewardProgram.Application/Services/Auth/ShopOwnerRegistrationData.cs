using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Auth;

internal record ShopOwnerRegistrationData(
    UserType UserType,
    string StoreName,
    string OwnerName,
    string MobileNumber,
    string VAT,
    string CRN,
    string ShopImageUrl,
    string CityId,
    string? DistrictId,
    string Street,
    int BuildingNumber,
    string PostalCode,
    int SubNumber,
    string AssignedSalesManId
);
