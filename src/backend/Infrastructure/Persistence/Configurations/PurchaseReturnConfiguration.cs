using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReturnConfiguration : BusinessDocumentConfigurationBase<PurchaseReturn>
{
    protected override void ConfigureDocument(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("purchase_returns");

        builder.Property(entity => entity.ReturnNo)
            .HasColumnName("return_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(entity => entity.ReferenceReceiptId)
            .HasColumnName("reference_receipt_id");

        builder.Property(entity => entity.ReturnDate)
            .HasColumnName("return_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.HasOne(entity => entity.Supplier)
            .WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ReferenceReceipt)
            .WithMany()
            .HasForeignKey(entity => entity.ReferenceReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Lines)
            .WithOne(entity => entity.PurchaseReturn)
            .HasForeignKey(entity => entity.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => entity.ReturnNo)
            .IsUnique();

        builder.HasIndex(entity => entity.ReturnDate);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.ReferenceReceiptId);
        builder.HasIndex(entity => entity.ReversalDocumentId);
        builder.HasIndex(entity => new { entity.SupplierId, entity.Status, entity.ReturnDate });
    }
}
