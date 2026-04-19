using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ItemConfiguration : AuditableEntityConfigurationBase<Item>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.BaseUomId)
            .HasColumnName("base_uom_id")
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(entity => entity.IsSellable)
            .HasColumnName("is_sellable")
            .IsRequired();

        builder.Property(entity => entity.IsComponent)
            .HasColumnName("is_component")
            .IsRequired();

        builder.HasOne(entity => entity.BaseUom)
            .WithMany()
            .HasForeignKey(entity => entity.BaseUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.Code)
            .IsUnique();

        builder.HasIndex(entity => entity.Name);
    }
}
