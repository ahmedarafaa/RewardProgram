namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminEditZoneManagerRequest(
    string Name,
    string MobileNumber,
    string RegionId
);
