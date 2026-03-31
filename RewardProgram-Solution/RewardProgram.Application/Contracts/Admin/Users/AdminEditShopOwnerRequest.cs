using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminEditShopOwnerRequest(
    string OwnerName,
    string CityId,
    string? StoreName,
    string? ShortAddress,
    IFormFile? ShopImage,
    NationalAddressResponse? NationalAddress
);
