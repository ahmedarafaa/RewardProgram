using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class ShopDataConfiguration : IEntityTypeConfiguration<ShopData>
{
    public void Configure(EntityTypeBuilder<ShopData> builder)
    {
        builder.ToTable("ShopData");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.StoreName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.VAT)
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(x => x.CRN)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.ShopImageUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.CityId)
            .IsRequired();

        builder.Property(x => x.Street)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PostalCode)
            .HasMaxLength(5)
            .IsRequired();

        builder.Property(x => x.EnteredByUserId)
            .HasMaxLength(450)
            .IsRequired();

        // Audit fields from TrackableEntity
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne(x => x.ErpCustomer)
            .WithOne(x => x.ShopData)
            .HasForeignKey<ShopData>(x => x.CustomerCode)
            .HasPrincipalKey<ErpCustomer>(x => x.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EnteredByUser)
            .WithMany()
            .HasForeignKey(x => x.EnteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.CustomerCode).IsUnique();
        builder.HasIndex(x => x.VAT).IsUnique();
        builder.HasIndex(x => x.CRN).IsUnique();
        builder.HasIndex(x => x.CityId);
        builder.HasIndex(x => x.StoreName);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
