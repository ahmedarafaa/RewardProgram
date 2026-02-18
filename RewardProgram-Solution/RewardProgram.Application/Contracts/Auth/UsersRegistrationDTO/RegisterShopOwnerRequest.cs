using Microsoft.AspNetCore.Http;

namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterShopOwnerRequest
(
    string StoreName,
    string OwnerName,
    string MobileNumber,
    string VAT,
    string CRN,
    string RegionId,
    string CityId,
    string? DistrictId,
    IFormFile ShopImage,
    // National Address
    NationalAddressResponse NationalAddress
);
