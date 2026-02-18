using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Auth;

internal record SellerRegistrationData(
    UserType UserType,
    string Name,
    string MobileNumber,
    string ShopCode,
    string ShopOwnerId,
    string AssignedSalesManId,
    string CityId,
    string? DistrictId,
    string Street,
    int BuildingNumber,
    string PostalCode,
    int SubNumber
);
