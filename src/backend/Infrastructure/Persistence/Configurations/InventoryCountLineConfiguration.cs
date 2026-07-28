using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class InventoryCountLineConfiguration : AuditableEntityConfigurationBase<InventoryCountLine>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<InventoryCountLine> builder)
    {
        builder.ToTable("inventory_count_lines");

        builder.Property(entity => entity.InventoryCountId)
            .HasColumnName("inventory_count_id")
            .IsRequired();

        builder.Property(entity => entity.LineNo)
            .HasColumnName("line_no")
            .IsRequired();

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.UomId)
            .HasColumnName("uom_id")
            .IsRequired();

        builder.Property(entity => entity.SystemQty)
            .HasColumnName("system_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.CountedQty)
            .HasColumnName("counted_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.VarianceQty)
            .HasColumnName("variance_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.BaseSystemQty)
            .HasColumnName("base_system_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.BaseCountedQty)
            .HasColumnName("base_counted_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.BaseVarianceQty)
            .HasColumnName("base_variance_qty")
            .HasColumnType("decimal(18,6)")
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

        builder.HasIndex(entity => new { entity.InventoryCountId, entity.LineNo })
            .IsUnique();

        builder.HasIndex(entity => new { entity.InventoryCountId, entity.ItemId })
            .IsUnique();

        builder.HasIndex(entity => entity.ItemId);
        builder.HasIndex(entity => entity.UomId);
    }
}
