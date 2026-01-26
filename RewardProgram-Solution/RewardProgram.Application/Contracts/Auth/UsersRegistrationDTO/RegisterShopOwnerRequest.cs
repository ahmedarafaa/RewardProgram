namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

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