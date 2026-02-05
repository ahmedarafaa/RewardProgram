using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Domain.Entities.Users;

public class District : TrackableEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public Zone Zone { get; set; }
    public string? ApprovalSalesManId { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation
    public City City { get; set; } = null!;
    public ApplicationUser? ApprovalSalesMan { get; set; }
    public List<ApplicationUser> SalesMen { get; set; } = []; // all salesmen in this district
}