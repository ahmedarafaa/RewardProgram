using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class ContactUsContentConfiguration : IEntityTypeConfiguration<ContactUsContent>
{
    public void Configure(EntityTypeBuilder<ContactUsContent> builder)
    {
        builder.ToTable("ContactUsContent");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.WhatsApp).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.WorkingHours).HasMaxLength(200).IsRequired();

        // Audit fields
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(450);
        builder.Property(x => x.UpdatedBy).HasMaxLength(450);
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
