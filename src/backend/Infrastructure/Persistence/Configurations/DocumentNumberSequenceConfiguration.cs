using ERP.Domain.System;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class DocumentNumberSequenceConfiguration : AuditableEntityConfigurationBase<DocumentNumberSequence>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<DocumentNumberSequence> builder)
    {
        builder.ToTable("document_number_sequences");

        builder.Property(entity => entity.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(entity => entity.Prefix)
            .HasColumnName("prefix")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(entity => entity.NextValue)
            .HasColumnName("next_value")
            .IsRequired();

        builder.Property(entity => entity.PaddingLength)
            .HasColumnName("padding_length")
            .IsRequired();

        builder.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.DocumentType })
            .IsUnique();
    }
}
