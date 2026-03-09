namespace RewardProgram.Application.Contracts.Admin.RewardSettings;

public record RewardSettingsResponse(
    string Id,
    decimal PointsToSarRate
);
