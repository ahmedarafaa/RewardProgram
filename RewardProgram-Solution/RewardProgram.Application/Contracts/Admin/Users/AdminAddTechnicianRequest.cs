namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminAddTechnicianRequest(
    string Name,
    string MobileNumber,
    string CityId,
    string PostalCode
);
