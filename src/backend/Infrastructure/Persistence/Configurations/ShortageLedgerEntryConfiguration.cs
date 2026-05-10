using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ShortageLedgerEntryConfiguration : AuditableEntityConfigurationBase<ShortageLedgerEntry>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ShortageLedgerEntry> builder)
    {
        builder.ToTable("shortage_ledger_entries");

        builder.Property(entity => entity.PurchaseReceiptId)
            .HasColumnName("purchase_receipt_id")
            .IsRequired();

        builder.Property(entity => entity.PurchaseReceiptLineId)
            .HasColumnName("purchase_receipt_line_id")
            .IsRequired();

        builder.Property(entity => entity.PurchaseOrderId)
            .HasColumnName("purchase_order_id");

        builder.Property(entity => entity.PurchaseOrderLineId)
            .HasColumnName("purchase_order_line_id");

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.ComponentItemId)
            .HasColumnName("component_item_id")
            .IsRequired();

        builder.Property(entity => entity.ExpectedQty)
            .HasColumnName("expected_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ActualQty)
            .HasColumnName("actual_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ShortageQty)
            .HasColumnName("shortage_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ResolvedPhysicalQty)
            .HasColumnName("resolved_physical_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ResolvedFinancialQtyEquivalent)
            .HasColumnName("resolved_financial_qty_equivalent")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.OpenQty)
            .HasColumnName("open_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.ShortageValue)
            .HasColumnName("shortage_value")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.ResolvedAmount)
            .HasColumnName("resolved_amount")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.OpenAmount)
            .HasColumnName("open_amount")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.ShortageReasonCodeId)
            .HasColumnName("shortage_reason_code_id");

        builder.Property(entity => entity.AffectsSupplierBalance)
            .HasColumnName("affects_supplier_balance")
            .IsRequired();

        builder.Property(entity => entity.ApprovalStatus)
            .HasColumnName("approval_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(entity => entity.PurchaseReceipt)
            .WithMany()
            .HasForeignKey(entity => entity.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.PurchaseReceiptLine)
            .WithMany()
            .HasForeignKey(entity => entity.PurchaseReceiptLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.PurchaseOrder)
            .WithMany()
            .HasForeignKey(entity => entity.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(entity => entity.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Item)
            .WithMany()
            .HasForeignKey(entity => entity.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ComponentItem)
            .WithMany()
            .HasForeignKey(entity => entity.ComponentItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ShortageReasonCode)
            .WithMany()
            .HasForeignKey(entity => entity.ShortageReasonCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.PurchaseReceiptId);
        builder.HasIndex(entity => entity.PurchaseReceiptLineId);
        builder.HasIndex(entity => entity.PurchaseOrderId);
        builder.HasIndex(entity => entity.PurchaseOrderLineId);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.ItemId);
        builder.HasIndex(entity => entity.ComponentItemId);
        builder.HasIndex(entity => new { entity.Status, entity.ComponentItemId });
        builder.HasIndex(entity => new { entity.Status, entity.ItemId });
        builder.HasIndex(entity => new { entity.PurchaseReceiptLineId, entity.ComponentItemId })
            .IsUnique();
    }
}
