using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : AuditableEntityConfigurationBase<Supplier>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.StatementName)
            .HasColumnName("statement_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);

        builder.Property(entity => entity.Email)
            .HasColumnName("email")
            .HasMaxLength(200);

        builder.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(entity => entity.Code)
            .IsUnique();

        builder.HasIndex(entity => entity.Name);
    }
}
