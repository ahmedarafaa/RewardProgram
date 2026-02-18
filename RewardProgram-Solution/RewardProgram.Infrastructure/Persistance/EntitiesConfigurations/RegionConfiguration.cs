using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.NameEn)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.ZoneManagerId)
            .HasMaxLength(450);

        // Audit fields
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Region → ZoneManager (one-to-one)
        builder.HasOne(x => x.ZoneManager)
            .WithOne(x => x.ManagedRegion)
            .HasForeignKey<Region>(x => x.ZoneManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => x.NameAr).IsUnique();
        builder.HasIndex(x => x.NameEn).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.ZoneManagerId).IsUnique()
            .HasFilter("[ZoneManagerId] IS NOT NULL");

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
