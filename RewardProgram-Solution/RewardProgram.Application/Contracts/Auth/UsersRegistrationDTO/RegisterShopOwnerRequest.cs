using Microsoft.AspNetCore.Http;

namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterShopOwnerRequest
(
    string StoreName,
    string OwnerName,
    string MobileNumber,
    string VAT,
    string CRN,
    IFormFile ShopImage,
    // National Address
    NationalAddressResponse NationalAddress
);