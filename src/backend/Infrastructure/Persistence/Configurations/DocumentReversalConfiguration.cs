using ERP.Domain.Reversals;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class DocumentReversalConfiguration : AuditableEntityConfigurationBase<DocumentReversal>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<DocumentReversal> builder)
    {
        builder.ToTable("document_reversals");

        builder.Property(entity => entity.ReversalNo)
            .HasColumnName("reversal_no")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.ReversedDocumentType)
            .HasColumnName("reversed_document_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.ReversedDocumentId)
            .HasColumnName("reversed_document_id")
            .IsRequired();

        builder.Property(entity => entity.ReversalDate)
            .HasColumnName("reversal_date")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.ReversalReason)
            .HasColumnName("reversal_reason")
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(entity => entity.ReversalNo)
            .IsUnique();

        builder.HasIndex(entity => new { entity.ReversedDocumentType, entity.ReversedDocumentId })
            .IsUnique();
    }
}
