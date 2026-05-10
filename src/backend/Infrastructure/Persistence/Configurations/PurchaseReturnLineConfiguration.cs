using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReturnLineConfiguration : AuditableEntityConfigurationBase<PurchaseReturnLine>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<PurchaseReturnLine> builder)
    {
        builder.ToTable("purchase_return_lines");

        builder.Property(entity => entity.PurchaseReturnId)
            .HasColumnName("purchase_return_id")
            .IsRequired();

        builder.Property(entity => entity.LineNo)
            .HasColumnName("line_no")
            .IsRequired();

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.ComponentId)
            .HasColumnName("component_id");

        builder.Property(entity => entity.WarehouseId)
            .HasColumnName("warehouse_id")
            .IsRequired();

        builder.Property(entity => entity.ReturnQty)
            .HasColumnName("return_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.UomId)
            .HasColumnName("uom_id")
            .IsRequired();

        builder.Property(entity => entity.BaseQty)
            .HasColumnName("base_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ReferenceReceiptLineId)
            .HasColumnName("reference_receipt_line_id");

        builder.HasOne(entity => entity.Item)
            .WithMany()
            .HasForeignKey(entity => entity.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Component)
            .WithMany()
            .HasForeignKey(entity => entity.ComponentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Uom)
            .WithMany()
            .HasForeignKey(entity => entity.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ReferenceReceiptLine)
            .WithMany()
            .HasForeignKey(entity => entity.ReferenceReceiptLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.PurchaseReturnId, entity.LineNo })
            .IsUnique();

        builder.HasIndex(entity => entity.ReferenceReceiptLineId);
    }
}
