using ERP.Domain.System;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ApplicationDatabaseUpgradeJournalConfiguration : AuditableEntityConfigurationBase<ApplicationDatabaseUpgradeJournal>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ApplicationDatabaseUpgradeJournal> builder)
    {
        builder.ToTable("application_database_upgrade_journal");

        builder.Property(entity => entity.StartedAtUtc)
            .HasColumnName("started_at_utc")
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(entity => entity.CompletedAtUtc)
            .HasColumnName("completed_at_utc")
            .HasColumnType("datetime2");

        builder.Property(entity => entity.FromSchemaVersion)
            .HasColumnName("from_schema_version")
            .IsRequired();

        builder.Property(entity => entity.TargetSchemaVersion)
            .HasColumnName("target_schema_version")
            .IsRequired();

        builder.Property(entity => entity.PreUpgradeBackupId)
            .HasColumnName("pre_upgrade_backup_id")
            .HasMaxLength(64);

        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.ApplicationVersion)
            .HasColumnName("application_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(entity => entity.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(64);

        builder.Property(entity => entity.FailureMessage)
            .HasColumnName("failure_message")
            .HasMaxLength(512);

        builder.Property(entity => entity.InstallationId)
            .HasColumnName("installation_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(entity => entity.StartedAtUtc);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.PreUpgradeBackupId);
    }
}
