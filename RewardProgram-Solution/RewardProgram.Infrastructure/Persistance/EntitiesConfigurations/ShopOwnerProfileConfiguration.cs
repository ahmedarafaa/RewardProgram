using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class ShopOwnerProfileConfiguration : IEntityTypeConfiguration<ShopOwnerProfile>
{
    public void Configure(EntityTypeBuilder<ShopOwnerProfile> builder)
    {
        builder.ToTable("ShopOwnerProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.CustomerCode)
            .HasMaxLength(50)
            .IsRequired();

        // Audit fields from TrackableEntity
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne(x => x.ErpCustomer)
            .WithMany(x => x.ShopOwners)
            .HasForeignKey(x => x.CustomerCode)
            .HasPrincipalKey(x => x.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.CustomerCode);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
