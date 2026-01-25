using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class ShopOwnerProfileConfiguration : IEntityTypeConfiguration<ShopOwnerProfile>
{
    public void Configure(EntityTypeBuilder<ShopOwnerProfile> builder)
    {
        builder.ToTable("ShopOwnerProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.StoreName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.VAT)
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(x => x.CRN)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.ShopCode)
            .HasMaxLength(6);

        builder.Property(x => x.ShopImageUrl)
            .HasMaxLength(500)
            .IsRequired();

        // Audit fields from TrackableEntity
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasMany(x => x.Sellers)
            .WithOne(x => x.ShopOwner)
            .HasForeignKey(x => x.ShopOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.ShopCode).IsUnique();
        builder.HasIndex(x => x.CRN).IsUnique();
        builder.HasIndex(x => x.VAT).IsUnique();
    }
}