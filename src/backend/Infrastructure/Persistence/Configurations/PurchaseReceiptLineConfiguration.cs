using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptLineConfiguration : AuditableEntityConfigurationBase<PurchaseReceiptLine>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("purchase_receipt_lines");

        builder.Property(entity => entity.PurchaseReceiptId)
            .HasColumnName("purchase_receipt_id")
            .IsRequired();

        builder.Property(entity => entity.LineNo)
            .HasColumnName("line_no")
            .IsRequired();

        builder.Property(entity => entity.PurchaseOrderLineId)
            .HasColumnName("purchase_order_line_id");

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.OrderedQtySnapshot)
            .HasColumnName("ordered_qty_snapshot")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.ReceivedQty)
            .HasColumnName("received_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.UomId)
            .HasColumnName("uom_id")
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.HasOne(entity => entity.Item)
            .WithMany()
            .HasForeignKey(entity => entity.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(entity => entity.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Uom)
            .WithMany()
            .HasForeignKey(entity => entity.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Components)
            .WithOne(entity => entity.PurchaseReceiptLine)
            .HasForeignKey(entity => entity.PurchaseReceiptLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.PurchaseReceiptId, entity.LineNo })
            .IsUnique();

        builder.HasIndex(entity => entity.PurchaseOrderLineId);
        builder.HasIndex(entity => entity.ItemId);
        builder.HasIndex(entity => entity.UomId);
        builder.HasIndex(entity => new { entity.PurchaseReceiptId, entity.ItemId });
    }
}
