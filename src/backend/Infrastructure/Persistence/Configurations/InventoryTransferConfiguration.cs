using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class InventoryTransferConfiguration : BusinessDocumentConfigurationBase<InventoryTransfer>
{
    protected override void ConfigureDocument(EntityTypeBuilder<InventoryTransfer> builder)
    {
        builder.ToTable("inventory_transfers");

        builder.Property(entity => entity.TransferNo)
            .HasColumnName("transfer_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.TransferDate)
            .HasColumnName("transfer_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.SourceWarehouseId)
            .HasColumnName("source_warehouse_id")
            .IsRequired();

        builder.Property(entity => entity.DestinationWarehouseId)
            .HasColumnName("destination_warehouse_id")
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

        builder.HasOne(entity => entity.SourceWarehouse)
            .WithMany()
            .HasForeignKey(entity => entity.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.DestinationWarehouse)
            .WithMany()
            .HasForeignKey(entity => entity.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Lines)
            .WithOne(entity => entity.InventoryTransfer)
            .HasForeignKey(entity => entity.InventoryTransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.TransferNo })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.TransferDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.SourceWarehouseId, entity.Status, entity.TransferDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.DestinationWarehouseId, entity.Status, entity.TransferDate });
    }
}
