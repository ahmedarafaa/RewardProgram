using Microsoft.AspNetCore.Http;

namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterShopOwnerRequest
(
    string CustomerCode,
    string OwnerName,
    string MobileNumber,
    string CityId,
    // Shop data fields — nullable (only required if ShopData doesn't exist for CustomerCode)
    string? StoreName,
    string? VAT,
    string? CRN,
    IFormFile? ShopImage,
    NationalAddressResponse? NationalAddress
);
