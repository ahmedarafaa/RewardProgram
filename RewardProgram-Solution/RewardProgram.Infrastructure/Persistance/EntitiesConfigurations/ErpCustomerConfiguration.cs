using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class ErpCustomerConfiguration : IEntityTypeConfiguration<ErpCustomer>
{
    public void Configure(EntityTypeBuilder<ErpCustomer> builder)
    {
        builder.ToTable("ErpCustomers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        // Audit fields from TrackableEntity
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Indexes
        builder.HasIndex(x => x.CustomerCode).IsUnique();

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
