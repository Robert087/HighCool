using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ItemUomConversionConfiguration : AuditableEntityConfigurationBase<ItemUomConversion>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ItemUomConversion> builder)
    {
        builder.ToTable("item_uom_conversions");

        builder.Property(entity => entity.ItemId)
            .HasColumnName("item_id")
            .IsRequired();

        builder.Property(entity => entity.FromUomId)
            .HasColumnName("from_uom_id")
            .IsRequired();

        builder.Property(entity => entity.ToUomId)
            .HasColumnName("to_uom_id")
            .IsRequired();

        builder.Property(entity => entity.Factor)
            .HasColumnName("factor")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.RoundingMode)
            .HasColumnName("rounding_mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entity => entity.MinFraction)
            .HasColumnName("min_fraction")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasOne(entity => entity.Item)
            .WithMany()
            .HasForeignKey(entity => entity.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.FromUom)
            .WithMany()
            .HasForeignKey(entity => entity.FromUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ToUom)
            .WithMany()
            .HasForeignKey(entity => entity.ToUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.ItemId, entity.FromUomId, entity.ToUomId, entity.IsActive })
            .IsUnique();
    }
}
