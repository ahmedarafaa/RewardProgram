using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminAddSellerRequest(
    string Name,
    string MobileNumber,
    string CustomerCode,
    string CityId,
    // Shop data fields — nullable (only required if ShopData doesn't exist for CustomerCode)
    string? StoreName,
    string? VAT,
    string? CRN,
    string? ShortAddress,
    IFormFile? ShopImage,
    NationalAddressResponse? NationalAddress
);
