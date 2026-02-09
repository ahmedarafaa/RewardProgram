namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterTechnicianRequest(
   string Name,
   string MobileNumber,
   string CityId,
   string DistrictId,
   string PostalCode
);
