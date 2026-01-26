using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Auth;

public record PendingUserResponse
(
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType,
    RegistrationStatus RegistrationStatus,
    DateTime RegisteredAt,
    NationalAddressResponse? NationalAddress,

    // ShopOwner specific
    string? StoreName,
    string? VAT,
    string? CRN,
    string? ShopImageUrl,

    //Seller specific,
    string? ShopOwnerName,
    string? ShopCode
);
