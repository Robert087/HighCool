using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ItemComponentConfiguration : AuditableEntityConfigurationBase<ItemComponent>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ItemComponent> builder)
    {
        builder.ToTable("item_components");

        builder.Property(entity => entity.ParentItemId)
            .HasColumnName("parent_item_id")
            .IsRequired();

        builder.Property(entity => entity.ComponentItemId)
            .HasColumnName("component_item_id")
            .IsRequired();

        builder.Property(entity => entity.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.HasOne(entity => entity.ParentItem)
            .WithMany()
            .HasForeignKey(entity => entity.ParentItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ComponentItem)
            .WithMany()
            .HasForeignKey(entity => entity.ComponentItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.ParentItemId, entity.ComponentItemId })
            .IsUnique();
    }
}
