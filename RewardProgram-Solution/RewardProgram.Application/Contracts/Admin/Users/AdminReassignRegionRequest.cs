namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminReassignRegionRequest(
    string RegionId,
    string ToZoneManagerId
);
