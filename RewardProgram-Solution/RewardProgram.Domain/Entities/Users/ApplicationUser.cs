using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Domain.Entities.Users;

public class ApplicationUser : IdentityUser
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7().ToString();
        SecurityStamp = Guid.CreateVersion7().ToString();
    }
    public string Name { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserType UserType { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public NationalAddress? NationalAddress { get; set; }

    // === ShopOwner/Seller/Technician → assigned SalesMan ===
    public string? AssignedSalesManId { get; set; }
    public ApplicationUser? AssignedSalesMan { get; set; }

    // Profiles (one-to-one, based on UserType)
    public ShopOwnerProfile? ShopOwnerProfile { get; set; }
    public SellerProfile? SellerProfile { get; set; }
    public TechnicianProfile? TechnicianProfile { get; set; }

    // Navigation properties
    public List<ApplicationUser> AssignedUsers { get; set; } = [];  // Users assigned to this SalesMan
    public List<ApprovalRecord> ApprovalRecords { get; set; } = [];
    public List<City> ApprovalCities { get; set; } = [];  // Cities where this user is ApprovalSalesMan
    public Region? ManagedRegion { get; set; }  // Region where this user is ZoneManager (inverse nav, no FK)
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

[Owned]
public class NationalAddress
{
    public string CityId { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public int BuildingNumber { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public int SubNumber { get; set; }
}
