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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserType UserType { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public NationalAddress? NationalAddress { get; set; }

    // === SalesMan Fields ===
    public string? DistrictId { get; set; } // SalesMan belongs to District
    public District? District { get; set; }
    public string? ZoneManagerId { get; set; } // SalesMan reports to ZoneManager
    public ApplicationUser? ZoneManager { get; set; }

    // ZoneManager assignment (for SalesMan only)
    public string? ManagedCityId { get; set; } // ZoneManager's City
    public City? ManagedCity { get; set; }
    public Zone? ManagedZone { get; set; } // ZoneManager's Zone


    // ShopOwner/Technician Fields 
    public string? AssignedSalesManId { get; set; }     
    public ApplicationUser? AssignedSalesMan { get; set; }

    // Profiles (one-to-one, based on UserType)
    public ShopOwnerProfile? ShopOwnerProfile { get; set; }
    public SellerProfile? SellerProfile { get; set; }
    public TechnicianProfile? TechnicianProfile { get; set; }

    // Navigation properties for assignments 
    public List<ApplicationUser> AssignedUsers { get; set; } = [];  // Users assigned to this SalesMan
    public List<ApplicationUser> ManagedSalesMen { get; set; } = [];  // SalesMen under this DM
    public List<ApprovalRecord> ApprovalRecords { get; set; } = [];
    public List<District> ApprovalDistricts { get; set; } = [];
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

[Owned]
public class NationalAddress
{
    public string CityId { get; set; } = string.Empty;
    public string DistrictId { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public int BuildingNumber { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public int SubNumber { get; set; }
}
