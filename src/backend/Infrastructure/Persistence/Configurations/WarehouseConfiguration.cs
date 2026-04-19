using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class WarehouseConfiguration : AuditableEntityConfigurationBase<Warehouse>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Location)
            .HasColumnName("location")
            .HasMaxLength(300);

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(entity => entity.Code)
            .IsUnique();

        builder.HasIndex(entity => entity.Name);
    }
}
