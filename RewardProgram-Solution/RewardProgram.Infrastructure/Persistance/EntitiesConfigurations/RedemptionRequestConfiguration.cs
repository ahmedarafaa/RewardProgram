using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class RedemptionRequestConfiguration : IEntityTypeConfiguration<RedemptionRequest>
{
    public void Configure(EntityTypeBuilder<RedemptionRequest> builder)
    {
        builder.ToTable("RedemptionRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.Method)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.PointsAmount)
            .HasColumnType("decimal(10,1)")
            .IsRequired();

        builder.Property(x => x.SarRate)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(x => x.SarAmount)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        // Bank transfer fields
        builder.Property(x => x.Iban)
            .HasMaxLength(34);

        builder.Property(x => x.AccountNumber)
            .HasMaxLength(50);

        builder.Property(x => x.Address)
            .HasMaxLength(200);

        builder.Property(x => x.SwiftCode)
            .HasMaxLength(11);

        builder.Property(x => x.AccountName)
            .HasMaxLength(200);

        // Cash handover fields
        builder.Property(x => x.CashOtpHash)
            .HasMaxLength(64);

        builder.Property(x => x.CashOtpEncrypted)
            .HasMaxLength(512);

        builder.Property(x => x.CashOtpAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.CashHandoverById)
            .HasMaxLength(450);

        // Rejection
        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.RejectedById)
            .HasMaxLength(450);

        // Concurrency control
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Audit fields
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CashHandoverBy)
            .WithMany()
            .HasForeignKey(x => x.CashHandoverById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RejectedBy)
            .WithMany()
            .HasForeignKey(x => x.RejectedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
