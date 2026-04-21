using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : AuditableEntityConfigurationBase<Customer>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);

        builder.Property(entity => entity.Email)
            .HasColumnName("email")
            .HasMaxLength(200);

        builder.Property(entity => entity.TaxNumber)
            .HasColumnName("tax_number")
            .HasMaxLength(64);

        builder.Property(entity => entity.Address)
            .HasColumnName("address")
            .HasMaxLength(500);

        builder.Property(entity => entity.City)
            .HasColumnName("city")
            .HasMaxLength(100);

        builder.Property(entity => entity.Area)
            .HasColumnName("area")
            .HasMaxLength(100);

        builder.Property(entity => entity.CreditLimit)
            .HasColumnName("credit_limit")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(entity => entity.PaymentTerms)
            .HasColumnName("payment_terms")
            .HasMaxLength(250);

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(entity => entity.Code)
            .IsUnique();

        builder.HasIndex(entity => entity.Name);
    }
}
