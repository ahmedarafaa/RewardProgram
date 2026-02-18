namespace RewardProgram.Domain.Entities.Users;

public class City : TrackableEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public string? ApprovalSalesManId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Region Region { get; set; } = null!;
    public ApplicationUser? ApprovalSalesMan { get; set; }
    public List<District> Districts { get; set; } = [];
}
