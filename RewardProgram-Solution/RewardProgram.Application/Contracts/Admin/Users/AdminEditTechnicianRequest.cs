namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminEditTechnicianRequest(
    string Name,
    string CityId,
    string PostalCode,
    string District
);
