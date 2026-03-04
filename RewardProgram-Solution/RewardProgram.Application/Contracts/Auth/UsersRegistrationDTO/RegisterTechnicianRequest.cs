namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterTechnicianRequest(
   string PinId,
   string Otp,
   string Name,
   string MobileNumber,
   string CityId,
   string PostalCode,
   string District
);
