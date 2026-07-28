using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class InventoryAdjustmentConfiguration : BusinessDocumentConfigurationBase<InventoryAdjustment>
{
    protected override void ConfigureDocument(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.ToTable("inventory_adjustments");

        builder.Property(entity => entity.AdjustmentNo)
            .HasColumnName("adjustment_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.AdjustmentDate)
            .HasColumnName("adjustment_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.WarehouseId)
            .HasColumnName("warehouse_id")
            .IsRequired();

        builder.Property(entity => entity.Reason)
            .HasColumnName("reason")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(entity => entity.PostedBy)
            .HasMaxLength(128);

        builder.Property(entity => entity.CanceledBy)
            .HasMaxLength(128);

        builder.HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Lines)
            .WithOne(entity => entity.InventoryAdjustment)
            .HasForeignKey(entity => entity.InventoryAdjustmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.AdjustmentNo })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.AdjustmentDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.WarehouseId, entity.Status, entity.AdjustmentDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Reason });
    }
}
