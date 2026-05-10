using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class StockLedgerEntryConfiguration : AuditableEntityConfigurationBase<StockLedgerEntry>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<StockLedgerEntry> builder)
    {
        builder.ToTable("stock_ledger_entries");

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.WarehouseId)
            .HasColumnName("warehouse_id")
            .IsRequired();

        builder.Property(entity => entity.TransactionType)
            .HasColumnName("transaction_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.SourceDocType)
            .HasColumnName("source_doc_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.SourceDocId)
            .HasColumnName("source_doc_id")
            .IsRequired();

        builder.Property(entity => entity.SourceLineId)
            .HasColumnName("source_line_id");

        builder.Property(entity => entity.QtyIn)
            .HasColumnName("qty_in")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.QtyOut)
            .HasColumnName("qty_out")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.UomId)
            .HasColumnName("uom_id")
            .IsRequired();

        builder.Property(entity => entity.BaseQty)
            .HasColumnName("base_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.RunningBalanceQty)
            .HasColumnName("running_balance_qty")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.TransactionDate)
            .HasColumnName("transaction_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.UnitCost)
            .HasColumnName("unit_cost")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.TotalCost)
            .HasColumnName("total_cost")
            .HasColumnType("decimal(18,6)");

        builder.HasOne(entity => entity.Item)
            .WithMany()
            .HasForeignKey(entity => entity.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Uom)
            .WithMany()
            .HasForeignKey(entity => entity.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.ItemId, entity.WarehouseId, entity.TransactionDate });
        builder.HasIndex(entity => new { entity.WarehouseId, entity.TransactionDate });
        builder.HasIndex(entity => new { entity.TransactionType, entity.TransactionDate });
        builder.HasIndex(entity => new { entity.SourceDocType, entity.SourceDocId });
        builder.HasIndex(entity => new { entity.SourceDocId, entity.SourceLineId, entity.TransactionType })
            .IsUnique();
    }
}
