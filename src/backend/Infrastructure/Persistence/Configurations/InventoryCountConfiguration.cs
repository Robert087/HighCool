using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class InventoryCountConfiguration : BusinessDocumentConfigurationBase<InventoryCount>
{
    protected override void ConfigureDocument(EntityTypeBuilder<InventoryCount> builder)
    {
        builder.ToTable("inventory_counts");

        builder.Property(entity => entity.CountNo)
            .HasColumnName("count_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.CountDate)
            .HasColumnName("count_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.SnapshotAt)
            .HasColumnName("snapshot_at")
            .HasColumnType("datetime2");

        builder.Property(entity => entity.WarehouseId)
            .HasColumnName("warehouse_id")
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
            .WithOne(entity => entity.InventoryCount)
            .HasForeignKey(entity => entity.InventoryCountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.CountNo })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.CountDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.WarehouseId, entity.Status, entity.CountDate });
    }
}
