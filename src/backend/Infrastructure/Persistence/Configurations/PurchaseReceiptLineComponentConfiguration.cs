using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptLineComponentConfiguration : AuditableEntityConfigurationBase<PurchaseReceiptLineComponent>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<PurchaseReceiptLineComponent> builder)
    {
        builder.ToTable("purchase_receipt_line_components");

        builder.Property(entity => entity.PurchaseReceiptLineId)
            .HasColumnName("purchase_receipt_line_id")
            .IsRequired();

        builder.Property(entity => entity.ComponentItemId)
            .HasColumnName("component_item_id")
            .IsRequired();

        builder.Property(entity => entity.ExpectedQty)
            .HasColumnName("expected_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ActualReceivedQty)
            .HasColumnName("actual_received_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.UomId)
            .HasColumnName("uom_id")
            .IsRequired();

        builder.Property(entity => entity.ShortageReasonCodeId)
            .HasColumnName("shortage_reason_code_id");

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.HasOne(entity => entity.ComponentItem)
            .WithMany()
            .HasForeignKey(entity => entity.ComponentItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Uom)
            .WithMany()
            .HasForeignKey(entity => entity.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ShortageReasonCode)
            .WithMany()
            .HasForeignKey(entity => entity.ShortageReasonCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.PurchaseReceiptLineId, entity.ComponentItemId })
            .IsUnique();
        builder.HasIndex(entity => entity.ComponentItemId);
        builder.HasIndex(entity => entity.ShortageReasonCodeId);
    }
}
