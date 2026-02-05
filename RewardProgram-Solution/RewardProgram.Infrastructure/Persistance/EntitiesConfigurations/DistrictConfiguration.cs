using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;
public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("Districts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.NameEn)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CityId)
            .IsRequired();

        builder.Property(x => x.Zone)
            .IsRequired();

        builder.Property(x => x.ApprovalSalesManId)
            .HasMaxLength(450);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Audit fields
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne(x => x.City)
            .WithMany(x => x.Districts)
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovalSalesMan)
            .WithMany(x => x.ApprovalDistricts)
            .HasForeignKey(x => x.ApprovalSalesManId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => x.CityId);
        builder.HasIndex(x => x.Zone);
        builder.HasIndex(x => x.ApprovalSalesManId);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.CityId, x.NameAr }).IsUnique();
        builder.HasIndex(x => new { x.CityId, x.NameEn }).IsUnique();

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}