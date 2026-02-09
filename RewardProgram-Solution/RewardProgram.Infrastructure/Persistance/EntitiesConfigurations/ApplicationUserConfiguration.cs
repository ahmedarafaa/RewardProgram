using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Primary key handled by Identity

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MobileNumber)
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(x => x.UserType)
            .IsRequired();

        builder.Property(x => x.RegistrationStatus)
            .IsRequired();

        builder.Property(x => x.IsDisabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Owned type: NationalAddress
        NationalAddressConfiguration(builder);

        // Owned type: RefreshTokens
        builder.OwnsMany(x => x.RefreshTokens, rt =>
        {
            rt.ToTable("RefreshTokens");
            rt.WithOwner().HasForeignKey("UserId");
            rt.Property(t => t.Token).HasMaxLength(500).IsRequired();
            rt.Property(t => t.ExpiresOn).IsRequired();
            rt.Property(t => t.CreatedOn).IsRequired();
            rt.HasKey("UserId", "Token");
            // Index for token lookup queries (RefreshTokenAsync, RevokeTokenAsync)
            rt.HasIndex(t => t.Token);
            // Index for cleanup queries
            rt.HasIndex(t => t.ExpiresOn);
            rt.HasIndex(t => t.RevokedOn);
        });

        // === SalesMan Fields ===
        builder.Property(x => x.DistrictId).HasMaxLength(450);
        builder.Property(x => x.ZoneManagerId).HasMaxLength(450);

        // === ZoneManager Fields ===
        builder.Property(x => x.ManagedCityId).HasMaxLength(450);
        builder.Property(x => x.ManagedZone);

        // SalesMan → District
        builder.HasOne(x => x.District)
            .WithMany(x => x.SalesMen)
            .HasForeignKey(x => x.DistrictId)
            .OnDelete(DeleteBehavior.SetNull);

        // SalesMan → ZoneManager
        builder.HasOne(x => x.ZoneManager)
            .WithMany(x => x.ManagedSalesMen)
            .HasForeignKey(x => x.ZoneManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ZoneManager → ManagedCity
        builder.HasOne(x => x.ManagedCity)
            .WithMany()
            .HasForeignKey(x => x.ManagedCityId)
            .OnDelete(DeleteBehavior.SetNull);

        // One ZoneManager per City + Zone
        builder.HasIndex(x => new { x.ManagedCityId, x.ManagedZone })
            .IsUnique()
            .HasFilter("[UserType] = 5 AND [ManagedCityId] IS NOT NULL AND [ManagedZone] IS NOT NULL");

        // ShopOwner/Technician → AssignedSalesMan
        builder.HasOne(x => x.AssignedSalesMan)
            .WithMany(x => x.AssignedUsers)
            .HasForeignKey(x => x.AssignedSalesManId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-one: Profiles
        builder.HasOne(x => x.ShopOwnerProfile)
            .WithOne(x => x.User)
            .HasForeignKey<ShopOwnerProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SellerProfile)
            .WithOne(x => x.User)
            .HasForeignKey<SellerProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TechnicianProfile)
            .WithOne(x => x.User)
            .HasForeignKey<TechnicianProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.MobileNumber).IsUnique();
        builder.HasIndex(x => x.UserType);
        builder.HasIndex(x => x.RegistrationStatus);
        builder.HasIndex(x => x.DistrictId);
        builder.HasIndex(x => x.ZoneManagerId);
        builder.HasIndex(x => x.ManagedCityId);
        builder.HasIndex(x => x.ManagedZone);
        builder.HasIndex(x => x.AssignedSalesManId);
        builder.HasIndex(x => x.IsDisabled);

        // Composite indexes
        builder.HasIndex(x => new { x.ManagedCityId, x.ManagedZone });
        builder.HasIndex(x => new { x.UserType, x.RegistrationStatus });
        builder.HasIndex(x => new { x.UserType, x.IsDisabled });
    }
    private void NationalAddressConfiguration(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.OwnsOne(x => x.NationalAddress, na =>
        {
            na.Property(a => a.BuildingNumber).HasColumnName("BuildingNumber");
            na.Property(a => a.CityId).HasColumnName("NationalAddress_CityId");
            na.Property(a => a.Street).HasColumnName("Street").HasMaxLength(100);
            na.Property(a => a.DistrictId).HasColumnName("NationalAddress_DistrictId").HasMaxLength(100);
            na.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(5);
            na.Property(a => a.SubNumber).HasColumnName("SubNumber");

            // Indexes
            na.HasIndex(a => a.CityId);
            na.HasIndex(a => a.DistrictId);
        });
    }
}