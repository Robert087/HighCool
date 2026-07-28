using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ItemCategoryConfiguration : AuditableEntityConfigurationBase<ItemCategory>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ItemCategory> builder)
    {
        builder.ToTable("item_categories");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Code })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.Name });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive, entity.Name });
    }
}
