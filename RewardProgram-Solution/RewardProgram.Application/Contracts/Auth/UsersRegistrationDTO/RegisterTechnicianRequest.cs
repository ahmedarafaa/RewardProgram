namespace RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

public record RegisterTechnicianRequest(
   string VerificationToken,
   string Name,
   string MobileNumber,
   string CityId,
   string District,
   string? InvitationCode
);
