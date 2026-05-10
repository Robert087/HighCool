using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderLineConfiguration : AuditableEntityConfigurationBase<PurchaseOrderLine>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines");

        builder.Property(entity => entity.PurchaseOrderId)
            .HasColumnName("purchase_order_id")
            .IsRequired();

        builder.Property(entity => entity.LineNo)
            .HasColumnName("line_no")
            .IsRequired();

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.OrderedQty)
            .HasColumnName("ordered_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.UnitPrice)
            .HasColumnName("unit_price")
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

        builder.HasOne(entity => entity.Uom)
            .WithMany()
            .HasForeignKey(entity => entity.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.PurchaseOrderId, entity.LineNo })
            .IsUnique();
        builder.HasIndex(entity => entity.ItemId);
        builder.HasIndex(entity => entity.UomId);
        builder.HasIndex(entity => new { entity.PurchaseOrderId, entity.ItemId });
    }
}
