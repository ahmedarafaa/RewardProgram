namespace RewardProgram.Domain.Entities;

public class RewardSettings : TrackableEntity
{
    public decimal PointsToSarRate { get; set; } = 10m;
    public decimal InviterRewardPoints { get; set; } = 100m;
    public decimal InviteeRewardPoints { get; set; } = 50m;
}
