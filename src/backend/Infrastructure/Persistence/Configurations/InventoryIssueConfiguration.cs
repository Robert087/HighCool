using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class InventoryIssueConfiguration : BusinessDocumentConfigurationBase<InventoryIssue>
{
    protected override void ConfigureDocument(EntityTypeBuilder<InventoryIssue> builder)
    {
        builder.ToTable("inventory_issues");

        builder.Property(entity => entity.IssueNo)
            .HasColumnName("issue_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.IssueDate)
            .HasColumnName("issue_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.WarehouseId)
            .HasColumnName("warehouse_id")
            .IsRequired();

        builder.Property(entity => entity.Reason)
            .HasColumnName("reason")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.ReferenceNo)
            .HasColumnName("reference_no")
            .HasMaxLength(64);

        builder.Property(entity => entity.RequestedBy)
            .HasColumnName("requested_by")
            .HasMaxLength(128);

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(entity => entity.PostedBy)
            .HasMaxLength(128);

        builder.Property(entity => entity.CanceledBy)
            .HasMaxLength(128);

        builder.HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entity => entity.Lines)
            .WithOne(entity => entity.InventoryIssue)
            .HasForeignKey(entity => entity.InventoryIssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.IssueNo })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.IssueDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.WarehouseId, entity.Status, entity.IssueDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Reason, entity.IssueDate });
    }
}
