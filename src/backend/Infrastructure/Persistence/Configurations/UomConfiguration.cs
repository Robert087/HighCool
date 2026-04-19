using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class UomConfiguration : AuditableEntityConfigurationBase<Uom>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<Uom> builder)
    {
        builder.ToTable("uoms");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.Precision)
            .HasColumnName("precision")
            .IsRequired();

        builder.Property(entity => entity.AllowsFraction)
            .HasColumnName("allows_fraction")
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(entity => entity.Code)
            .IsUnique();

        builder.HasIndex(entity => entity.Name);
    }
}
