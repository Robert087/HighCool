using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ItemConfiguration : AuditableEntityConfigurationBase<Item>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.BaseUomId)
            .HasColumnName("base_uom_id")
            .IsRequired();

        builder.Property(entity => entity.CategoryId)
            .HasColumnName("category_id");

        builder.Property(entity => entity.DefaultWarehouseId)
            .HasColumnName("default_warehouse_id");

        builder.Property(entity => entity.MinimumStockQuantity)
            .HasColumnName("minimum_stock_quantity")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(entity => entity.IsSellable)
            .HasColumnName("is_sellable")
            .IsRequired();

        builder.Property(entity => entity.HasComponents)
            .HasColumnName("has_components")
            .IsRequired();

        builder.HasOne(entity => entity.BaseUom)
            .WithMany()
            .HasForeignKey(entity => entity.BaseUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Category)
            .WithMany()
            .HasForeignKey(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.DefaultWarehouse)
            .WithMany()
            .HasForeignKey(entity => entity.DefaultWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Components)
            .WithOne(entity => entity.Item)
            .HasForeignKey(entity => entity.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Code })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Name });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive, entity.Name });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CategoryId });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.BaseUomId });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.DefaultWarehouseId });
    }
}
