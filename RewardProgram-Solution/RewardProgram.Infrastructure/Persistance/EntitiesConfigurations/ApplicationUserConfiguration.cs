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

        // Owned type: NationalAddress
        NationalAddressConfiguration(builder);

        // Owned type: RefreshTokens
        builder.OwnsMany(x => x.RefreshTokens, rt =>
        {
            rt.WithOwner().HasForeignKey("UserId");
            rt.Property(t => t.Token).HasMaxLength(500);
            rt.Property(t => t.CreatedOn).IsRequired();
            rt.Property(t => t.ExpiresOn).IsRequired();
        });

        // Self-referencing: AssignedSalesMan
        builder.HasOne(x => x.AssignedSalesMan)
            .WithMany(x => x.AssignedUsers)
            .HasForeignKey(x => x.AssignedSalesManId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing: DistrictManager
        builder.HasOne(x => x.DistrictManager)
            .WithMany(x => x.ManagedSalesMen)
            .HasForeignKey(x => x.DistrictManagerId)
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
    }
private void NationalAddressConfiguration(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.OwnsOne(x => x.NationalAddress, na =>
        {
            na.Property(a => a.BuildingNumber).HasColumnName("BuildingNumber");
            na.Property(a => a.City).HasColumnName("City").HasMaxLength(50);
            na.Property(a => a.Street).HasColumnName("Street").HasMaxLength(100);
            na.Property(a => a.neighborhood).HasColumnName("Neighborhood").HasMaxLength(100);
            na.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(5);
            na.Property(a => a.subNumber).HasColumnName("SubNumber");
        });
    }
}