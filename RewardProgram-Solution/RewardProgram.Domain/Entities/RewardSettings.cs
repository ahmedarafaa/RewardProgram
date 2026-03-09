namespace RewardProgram.Domain.Entities;

public class RewardSettings : TrackableEntity
{
    public decimal PointsToSarRate { get; set; } = 10m;
}
