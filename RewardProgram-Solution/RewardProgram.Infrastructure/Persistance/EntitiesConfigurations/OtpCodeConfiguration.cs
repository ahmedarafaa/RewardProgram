using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RewardProgram.Domain.Entities.OTP;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Persistance.EntitiesConfigurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("OtpCodes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PinId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MobileNumber)
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(x => x.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.RegistrationData)
            .HasColumnType("nvarchar(max)");

        // SMS fallback fields. CurrentSid mirrors PinId on insert but is rotated
        // by OtpFallbackWorker when WhatsApp delivery hasn't been verified within
        // the configured window.
        builder.Property(x => x.CurrentSid)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue("whatsapp");

        builder.Property(x => x.FallbackFired)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(x => x.PinId).IsUnique();
        builder.HasIndex(x => x.MobileNumber);
        builder.HasIndex(x => x.IsUsed);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.MobileNumber, x.IsUsed });
        builder.HasIndex(x => new { x.PinId, x.IsUsed });

        // Composite index that exactly matches the fallback worker's predicate:
        // rows due, not yet fired, not yet used, on the WhatsApp channel.
        builder.HasIndex(x => new { x.FallbackFired, x.IsUsed, x.FallbackEligibleAt })
            .HasDatabaseName("IX_OtpCodes_FallbackDue");
    }
}