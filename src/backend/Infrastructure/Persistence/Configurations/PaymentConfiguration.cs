using ERP.Domain.Payments;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : BusinessDocumentConfigurationBase<Payment>
{
    protected override void ConfigureDocument(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.Property(entity => entity.PaymentNo)
            .HasColumnName("payment_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.PartyType)
            .HasColumnName("party_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entity => entity.PartyId)
            .HasColumnName("party_id")
            .IsRequired();

        builder.Property(entity => entity.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(entity => entity.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.PaymentDate)
            .HasColumnName("payment_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(16);

        builder.Property(entity => entity.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(entity => entity.ReferenceNote)
            .HasColumnName("reference_note")
            .HasMaxLength(128);

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.HasOne(entity => entity.Supplier)
            .WithMany()
            .HasForeignKey(entity => entity.PartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Allocations)
            .WithOne(entity => entity.Payment)
            .HasForeignKey(entity => entity.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => entity.PaymentNo)
            .IsUnique();

        builder.HasIndex(entity => new { entity.PartyType, entity.PartyId, entity.PaymentDate });
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.PaymentMethod);
        builder.HasIndex(entity => entity.Direction);
    }
}
