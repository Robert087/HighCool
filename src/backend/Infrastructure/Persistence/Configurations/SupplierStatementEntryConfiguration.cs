using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class SupplierStatementEntryConfiguration : AuditableEntityConfigurationBase<SupplierStatementEntry>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<SupplierStatementEntry> builder)
    {
        builder.ToTable("supplier_statement_entries");

        builder.Property(entity => entity.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(entity => entity.EffectType)
            .HasColumnName("effect_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(entity => entity.SourceDocType)
            .HasColumnName("source_doc_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(entity => entity.SourceDocId)
            .HasColumnName("source_doc_id")
            .IsRequired();

        builder.Property(entity => entity.SourceLineId)
            .HasColumnName("source_line_id");

        builder.Property(entity => entity.Debit)
            .HasColumnName("debit")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.Credit)
            .HasColumnName("credit")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.RunningBalance)
            .HasColumnName("running_balance")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(16);

        builder.Property(entity => entity.EntryDate)
            .HasColumnName("entry_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.HasOne(entity => entity.Supplier)
            .WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.SupplierId, entity.EntryDate });
        builder.HasIndex(entity => new { entity.SourceDocType, entity.SourceDocId, entity.SourceLineId, entity.EffectType })
            .IsUnique();
    }
}
