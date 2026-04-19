using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Base;

public abstract class BusinessDocumentConfigurationBase<TEntity> : AuditableEntityConfigurationBase<TEntity>
    where TEntity : BusinessDocument
{
    protected sealed override void ConfigureAuditableEntity(EntityTypeBuilder<TEntity> builder)
    {
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        ConfigureDocument(builder);
    }

    protected abstract void ConfigureDocument(EntityTypeBuilder<TEntity> builder);
}
