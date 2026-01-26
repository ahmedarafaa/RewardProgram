namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterTechnicianRequest
(
   string Name,
   string MobileNumber,
   NationalAddressResponse NationalAddress
);
