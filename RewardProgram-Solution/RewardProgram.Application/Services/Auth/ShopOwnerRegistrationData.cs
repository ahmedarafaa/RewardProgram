using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Auth;

internal record ShopOwnerRegistrationData(
    string StoreName,
    string OwnerName,
    string MobileNumber,
    string VAT,
    string CRN,
    string ShopImageUrl,
    string CityId,
    string DistrictId,
    Zone Zone,
    string Street,
    int BuildingNumber,
    string PostalCode,
    int SubNumber,
    string AssignedSalesManId
);
