using ERP.Domain.System;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ApplicationDatabaseMetadataConfiguration : AuditableEntityConfigurationBase<ApplicationDatabaseMetadata>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ApplicationDatabaseMetadata> builder)
    {
        builder.ToTable("application_database_metadata");

        builder.Property(entity => entity.ApplicationVersion)
            .HasColumnName("application_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(entity => entity.InstallationId)
            .HasColumnName("installation_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(entity => entity.DatabaseSchemaVersion)
            .HasColumnName("database_schema_version")
            .IsRequired();

        builder.Property(entity => entity.DatabaseCreatedAtUtc)
            .HasColumnName("database_created_at_utc")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.LastSuccessfulSchemaUpgradeAtUtc)
            .HasColumnName("last_successful_schema_upgrade_at_utc")
            .HasColumnType("datetime2")
            .IsRequired();
    }
}
