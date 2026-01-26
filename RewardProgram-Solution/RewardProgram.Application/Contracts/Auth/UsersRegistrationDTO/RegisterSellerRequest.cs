namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterSellerRequest
(
     string Name,
     string MobileNumber ,
     string ShopCode, // To link with ShopOwner
     NationalAddressResponse NationalAddress
);

