using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ShortageResolutionConfiguration : BusinessDocumentConfigurationBase<ShortageResolution>
{
    protected override void ConfigureDocument(EntityTypeBuilder<ShortageResolution> builder)
    {
        builder.ToTable("shortage_resolutions");

        builder.Property(entity => entity.ResolutionNo)
            .HasColumnName("resolution_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(entity => entity.ResolutionType)
            .HasColumnName("resolution_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entity => entity.ResolutionDate)
            .HasColumnName("resolution_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.TotalQty)
            .HasColumnName("total_qty")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(16);

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(entity => entity.ApprovedBy)
            .HasColumnName("approved_by")
            .HasMaxLength(128);

        builder.HasOne(entity => entity.Supplier)
            .WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Allocations)
            .WithOne(entity => entity.Resolution)
            .HasForeignKey(entity => entity.ResolutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => entity.ResolutionNo)
            .IsUnique();
        builder.HasIndex(entity => new { entity.SupplierId, entity.ResolutionType, entity.Status, entity.ResolutionDate });
        builder.HasIndex(entity => new { entity.SupplierId, entity.Status, entity.ResolutionDate });
        builder.HasIndex(entity => entity.ReversalDocumentId);
    }
}
