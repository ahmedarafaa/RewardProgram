namespace RewardProgram.Application.Contracts.Admin.RewardSettings;

public record UpdateRewardSettingsRequest(
    decimal PointsToSarRate,
    decimal InviterRewardPoints,
    decimal InviteeRewardPoints
);
