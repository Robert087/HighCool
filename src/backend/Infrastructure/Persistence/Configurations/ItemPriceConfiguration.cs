using ERP.Domain.Pricing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ItemPriceConfiguration : AuditableEntityConfigurationBase<ItemPrice>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ItemPrice> builder)
    {
        builder.ToTable("item_prices");

        builder.Property(entity => entity.PriceListId)
            .HasColumnName("price_list_id")
            .IsRequired();

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.UomId)
            .HasColumnName("uom_id")
            .IsRequired();

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(entity => entity.Rate)
            .HasColumnName("rate")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.MinimumQuantity)
            .HasColumnName("minimum_quantity")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.ValidTo)
            .HasColumnName("valid_to")
            .HasColumnType("datetime2");

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne(entity => entity.Item)
            .WithMany()
            .HasForeignKey(entity => entity.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Uom)
            .WithMany()
            .HasForeignKey(entity => entity.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.PriceListId, entity.ItemId, entity.UomId, entity.MinimumQuantity, entity.IsActive });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.PriceListId, entity.ItemId, entity.UomId, entity.IsActive, entity.ValidFrom, entity.ValidTo, entity.MinimumQuantity });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.ItemId, entity.UomId });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Currency });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive, entity.ValidFrom, entity.ValidTo });
    }
}
