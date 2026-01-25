using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class CitySalesManMappingConfiguration : IEntityTypeConfiguration<CitySalesManMapping>
{
    public void Configure(EntityTypeBuilder<CitySalesManMapping> builder)
    {
        builder.ToTable("CitySalesManMappings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.City)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SalesManId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Audit fields from TrackableEntity
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne(x => x.SalesMan)
            .WithMany()
            .HasForeignKey(x => x.SalesManId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.City);
        builder.HasIndex(x => x.SalesManId);
        builder.HasIndex(x => new { x.City, x.IsActive });
    }
}