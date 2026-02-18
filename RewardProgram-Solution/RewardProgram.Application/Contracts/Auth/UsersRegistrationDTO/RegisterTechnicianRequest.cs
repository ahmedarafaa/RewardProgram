namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterTechnicianRequest(
   string Name,
   string MobileNumber,
   string RegionId,
   string CityId,
   string? DistrictId,
   string PostalCode
);
