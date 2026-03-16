using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class RewardSettingsConfiguration : IEntityTypeConfiguration<RewardSettings>
{
    public void Configure(EntityTypeBuilder<RewardSettings> builder)
    {
        builder.ToTable("RewardSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PointsToSarRate)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(x => x.InviterRewardPoints)
            .HasColumnType("decimal(10,1)")
            .HasDefaultValue(100m)
            .IsRequired();

        builder.Property(x => x.InviteeRewardPoints)
            .HasColumnType("decimal(10,1)")
            .HasDefaultValue(50m)
            .IsRequired();

        builder.Property(x => x.MinimumRedemptionPoints)
            .HasColumnType("decimal(10,1)")
            .HasDefaultValue(1000m)
            .IsRequired();

        // Audit fields
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
