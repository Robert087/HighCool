using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderConfiguration : BusinessDocumentConfigurationBase<PurchaseOrder>
{
    protected override void ConfigureDocument(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.Property(entity => entity.PoNo)
            .HasColumnName("po_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(entity => entity.OrderDate)
            .HasColumnName("order_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.ExpectedDate)
            .HasColumnName("expected_date")
            .HasColumnType("datetime2");

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.HasOne(entity => entity.Supplier)
            .WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Lines)
            .WithOne(entity => entity.PurchaseOrder)
            .HasForeignKey(entity => entity.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => entity.PoNo)
            .IsUnique();

        builder.HasIndex(entity => entity.OrderDate);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => new { entity.SupplierId, entity.Status, entity.OrderDate });
        builder.HasIndex(entity => new { entity.SupplierId, entity.OrderDate });
    }
}
