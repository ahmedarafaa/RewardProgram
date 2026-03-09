using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class ScanRecordConfiguration : IEntityTypeConfiguration<ScanRecord>
{
    public void Configure(EntityTypeBuilder<ScanRecord> builder)
    {
        builder.ToTable("ScanRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BarcodeId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.ScannerRole)
            .IsRequired();

        builder.Property(x => x.PointsAwarded)
            .HasColumnType("decimal(8,1)")
            .IsRequired();

        // Audit fields
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne(x => x.Barcode)
            .WithMany(x => x.ScanRecords)
            .HasForeignKey(x => x.BarcodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite unique: one scan per role per barcode
        builder.HasIndex(x => new { x.BarcodeId, x.ScannerRole }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
