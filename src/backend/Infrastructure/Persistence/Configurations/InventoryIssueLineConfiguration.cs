using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class InventoryIssueLineConfiguration : AuditableEntityConfigurationBase<InventoryIssueLine>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<InventoryIssueLine> builder)
    {
        builder.ToTable("inventory_issue_lines");

        builder.Property(entity => entity.InventoryIssueId)
            .HasColumnName("inventory_issue_id")
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

        builder.Property(entity => entity.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.BaseQty)
            .HasColumnName("base_qty")
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

        builder.HasIndex(entity => new { entity.InventoryIssueId, entity.LineNo })
            .IsUnique();

        builder.HasIndex(entity => new { entity.InventoryIssueId, entity.ItemId })
            .IsUnique();

        builder.HasIndex(entity => entity.ItemId);
        builder.HasIndex(entity => entity.UomId);
    }
}
