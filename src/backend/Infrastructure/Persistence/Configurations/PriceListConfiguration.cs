using ERP.Domain.Pricing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PriceListConfiguration : AuditableEntityConfigurationBase<PriceList>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_lists");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(entity => entity.IsDefault)
            .HasColumnName("is_default")
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasMany(entity => entity.ItemPrices)
            .WithOne(entity => entity.PriceList)
            .HasForeignKey(entity => entity.PriceListId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Code })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Type, entity.IsDefault, entity.IsActive });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Type, entity.IsActive, entity.Name });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Currency });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Name });
    }
}
