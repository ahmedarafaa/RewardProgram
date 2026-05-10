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
    public string? ProfileImageUrl { get; set; }
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

    // === Invitation ===
    public string? InvitationCode { get; set; }
    public string? InvitedByUserId { get; set; }
    public ApplicationUser? InvitedByUser { get; set; }
    // Number of invitation rewards already credited to this user as inviter.
    // Guarded by an atomic UPDATE with a `< MaxRewardedInvitations` predicate
    // to serialize cap enforcement without a table lock.
    public int InviterRewardCount { get; set; }

    // === FCM Push Notifications ===
    public string? FcmToken { get; set; }

    // === Account Deletion ===
    public bool IsAccountDeleted { get; set; }
    public DateTime? AccountDeletedAt { get; set; }
    // Null = self-deleted via the public delete-account endpoint.
    // Set = soft-deleted by an admin (the admin's user-id is recorded for audit).
    // Distinction matters because admin restore should treat the two cases differently
    // — see `AdminUserService.RestoreUserAsync` and the front-end "self-deleted" hint.
    public string? DeletedByAdminId { get; set; }
    public DateTime? RestoredAt { get; set; }
    public string? RestoredByAdminId { get; set; }
}

[Owned]
public class NationalAddress
{
    public string CityId { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public int BuildingNumber { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public int SubNumber { get; set; }
    public string District { get; set; } = string.Empty;
}
