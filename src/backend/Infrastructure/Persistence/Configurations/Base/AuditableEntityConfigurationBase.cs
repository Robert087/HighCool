using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Base;

public abstract class AuditableEntityConfigurationBase<TEntity> : EntityConfigurationBase<TEntity>
    where TEntity : AuditableEntity
{
    protected sealed override void ConfigureEntity(EntityTypeBuilder<TEntity> builder)
    {
        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2");

        builder.Property(entity => entity.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(128);

        ConfigureAuditableEntity(builder);
    }

    protected abstract void ConfigureAuditableEntity(EntityTypeBuilder<TEntity> builder);
}
