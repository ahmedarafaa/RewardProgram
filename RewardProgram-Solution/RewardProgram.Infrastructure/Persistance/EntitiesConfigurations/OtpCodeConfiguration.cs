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

        // SMS fallback fields. CurrentSid mirrors PinId on insert and is rotated
        // to the SMS-channel Sid when fallback fires (sync error at send time, or
        // Twilio delivery webhook later). PinId stays untouched so the mobile
        // app's public token remains valid across the channel switch.
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

        // SQL Server rowversion column — auto-managed, increments on every UPDATE.
        // EF treats it as a concurrency token so a stale write fails fast with
        // DbUpdateConcurrencyException instead of clobbering the other request.
        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Indexes
        builder.HasIndex(x => x.PinId).IsUnique();
        builder.HasIndex(x => x.MobileNumber);
        builder.HasIndex(x => x.IsUsed);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.MobileNumber, x.IsUsed });
        builder.HasIndex(x => new { x.PinId, x.IsUsed });

        // The webhook handler looks up the row by Twilio's VerificationSid (which
        // == CurrentSid). Indexed for the lookup hot path on each webhook hit.
        builder.HasIndex(x => x.CurrentSid)
            .HasDatabaseName("IX_OtpCodes_CurrentSid");
    }
}