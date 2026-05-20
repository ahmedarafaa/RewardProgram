using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class AdminUserPermissionConfiguration : IEntityTypeConfiguration<AdminUserPermission>
{
    public void Configure(EntityTypeBuilder<AdminUserPermission> builder)
    {
        builder.ToTable("AdminUserPermissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.Permission)
            .HasMaxLength(64)
            .IsRequired();

        // One row per (user, permission).
        builder.HasIndex(x => new { x.UserId, x.Permission }).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
