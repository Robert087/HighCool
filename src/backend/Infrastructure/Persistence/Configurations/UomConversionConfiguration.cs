using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class UomConversionConfiguration : AuditableEntityConfigurationBase<UomConversion>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<UomConversion> builder)
    {
        builder.ToTable("uom_conversions");

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

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasOne(entity => entity.FromUom)
            .WithMany()
            .HasForeignKey(entity => entity.FromUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.ToUom)
            .WithMany()
            .HasForeignKey(entity => entity.ToUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => new { entity.OrganizationId, entity.FromUomId, entity.ToUomId, entity.IsActive })
            .IsUnique();

        builder.HasIndex(entity => new { entity.OrganizationId, entity.ToUomId });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive });
    }
}
