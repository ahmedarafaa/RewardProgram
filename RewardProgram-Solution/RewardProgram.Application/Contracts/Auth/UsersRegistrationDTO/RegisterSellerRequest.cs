using Microsoft.AspNetCore.Http;

namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterSellerRequest(
    string PinId,
    string Otp,
    string Name,
    string MobileNumber,
    string CustomerCode,
    // Shop data fields — nullable (only required if ShopData doesn't exist for CustomerCode)
    string? StoreName,
    string? VAT,
    string? CRN,
    string? ShortAddress,
    IFormFile? ShopImage,
    string? CityId,
    NationalAddressResponse? NationalAddress,
    string? InvitationCode
);
