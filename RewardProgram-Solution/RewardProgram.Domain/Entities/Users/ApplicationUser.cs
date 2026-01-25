using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

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

    public UserType UserType { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public NationalAddress? NationalAddress { get; set; }

    // SalesMan assignment (for ShopOwner, Seller, Technician)
    public string? AssignedSalesManId { get; set; }
    public ApplicationUser? AssignedSalesMan { get; set; }
    public List<ApplicationUser> AssignedUsers { get; set; } = [];  // Users assigned to this SalesMan

    // DistrictManager assignment (for SalesMan only)
    public string? DistrictManagerId { get; set; }
    public ApplicationUser? DistrictManager { get; set; }
    public List<ApplicationUser> ManagedSalesMen { get; set; } = [];  // SalesMen under this DM

    // Profiles (one-to-one, based on UserType)
    public ShopOwnerProfile? ShopOwnerProfile { get; set; }
    public SellerProfile? SellerProfile { get; set; }
    public TechnicianProfile? TechnicianProfile { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<ApprovalRecord> ApprovalRecords { get; set; } = [];
}

[Owned]
public class NationalAddress
{
    public int BuildingNumber { get; set; }
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public int SubNumber { get; set; }
}
