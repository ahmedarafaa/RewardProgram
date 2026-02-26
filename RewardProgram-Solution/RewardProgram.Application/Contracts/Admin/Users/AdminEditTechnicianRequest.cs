namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminEditTechnicianRequest(
    string Name,
    string MobileNumber,
    string CityId,
    string PostalCode
);
