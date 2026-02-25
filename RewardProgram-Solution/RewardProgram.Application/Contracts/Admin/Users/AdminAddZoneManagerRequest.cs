namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminAddZoneManagerRequest(
    string Name,
    string MobileNumber,
    string RegionId
);
