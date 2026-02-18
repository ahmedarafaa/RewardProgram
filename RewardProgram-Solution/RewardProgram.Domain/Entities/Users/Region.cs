namespace RewardProgram.Domain.Entities.Users;

public class Region : TrackableEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? ZoneManagerId { get; set; }

    // Navigation
    public ApplicationUser? ZoneManager { get; set; }
    public List<City> Cities { get; set; } = [];
}
