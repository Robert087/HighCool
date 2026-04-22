using ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Infrastructure.Persistence.Configurations.Base;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptConfiguration : BusinessDocumentConfigurationBase<PurchaseReceipt>
{
    protected override void ConfigureDocument(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("purchase_receipts");

        builder.Property(entity => entity.ReceiptNo)
            .HasColumnName("receipt_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(entity => entity.WarehouseId)
            .HasColumnName("warehouse_id")
            .IsRequired();

        builder.Property(entity => entity.PurchaseOrderId)
            .HasColumnName("purchase_order_id");

        builder.Property(entity => entity.ReceiptDate)
            .HasColumnName("receipt_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.SupplierPayableAmount)
            .HasColumnName("supplier_payable_amount")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.HasOne(entity => entity.Supplier)
            .WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.PurchaseOrder)
            .WithMany()
            .HasForeignKey(entity => entity.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Lines)
            .WithOne(entity => entity.PurchaseReceipt)
            .HasForeignKey(entity => entity.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => entity.ReceiptNo)
            .IsUnique();

        builder.HasIndex(entity => entity.ReceiptDate);

        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.PurchaseOrderId);
    }
}
