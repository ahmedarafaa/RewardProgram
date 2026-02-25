using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminAddShopOwnerRequest(
    string OwnerName,
    string MobileNumber,
    string CustomerCode,
    string CityId,
    // Shop data fields — nullable (only required if ShopData doesn't exist for CustomerCode)
    string? StoreName,
    string? VAT,
    string? CRN,
    IFormFile? ShopImage,
    NationalAddressResponse? NationalAddress
);
