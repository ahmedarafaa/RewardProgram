using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminEditSellerRequest(
    string Name,
    string MobileNumber,
    string CustomerCode,
    string CityId,
    string? StoreName,
    string? VAT,
    string? CRN,
    IFormFile? ShopImage,
    NationalAddressResponse? NationalAddress
);
