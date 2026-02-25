using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;

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
            rt.HasIndex(t => t.Token);
            rt.HasIndex(t => t.ExpiresOn);
            rt.HasIndex(t => t.RevokedOn);
        });

        // ShopOwner/Seller/Technician → AssignedSalesMan
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

        // Region.ZoneManagerId → ApplicationUser.ManagedRegion is configured in RegionConfiguration

        // Indexes
        builder.HasIndex(x => x.MobileNumber).IsUnique();
        builder.HasIndex(x => x.UserType);
        builder.HasIndex(x => x.RegistrationStatus);
        builder.HasIndex(x => x.AssignedSalesManId);
        builder.HasIndex(x => x.IsDisabled);

        // Composite indexes
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
            na.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(5);
            na.Property(a => a.SubNumber).HasColumnName("SubNumber");

            // Indexes
            na.HasIndex(a => a.CityId);
        });
    }
}
