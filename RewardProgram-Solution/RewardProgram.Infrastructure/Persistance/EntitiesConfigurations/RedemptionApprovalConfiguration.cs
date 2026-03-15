using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class RedemptionApprovalConfiguration : IEntityTypeConfiguration<RedemptionApproval>
{
    public void Configure(EntityTypeBuilder<RedemptionApproval> builder)
    {
        builder.ToTable("RedemptionApprovals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RedemptionRequestId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.ApproverId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.Action)
            .IsRequired();

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.FromStatus)
            .IsRequired();

        builder.Property(x => x.ToStatus)
            .IsRequired();

        // Audit fields
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne(x => x.RedemptionRequest)
            .WithMany(x => x.Approvals)
            .HasForeignKey(x => x.RedemptionRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.RedemptionRequestId);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
