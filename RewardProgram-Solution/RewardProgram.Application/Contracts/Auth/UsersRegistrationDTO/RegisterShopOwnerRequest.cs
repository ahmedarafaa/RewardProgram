using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.DTOs.Auth.UsersDTO;

public record RegisterShopOwnerRequest
(
    string OwnerName,
    string MobileNumber,
    string StoreName,
    string VAT,
    string CRN,
    string ShopImageUrl,

    // National Address
    NationalAddressResponse NationalAddress
);