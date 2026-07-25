using ERP.Application.LocalData;
using ERP.Domain.System;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ERP.Infrastructure.LocalData;

public sealed class DatabaseUpgradeService(
    AppDbContext dbContext,
    IDatabaseConfigurationService databaseConfigurationService,
    IApplicationDatabaseMetadataService metadataService,
    IDatabaseBackupService backupService) : IDatabaseUpgradeService
{
    private static readonly SemaphoreSlim UpgradeLock = new(1, 1);

    public async Task<DatabaseUpgradeResult> UpgradeAsync(
        DatabaseUpgradeRequest request,
        CancellationToken cancellationToken)
    {
        if (!await UpgradeLock.WaitAsync(0, cancellationToken))
        {
            return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Rejected, "A database upgrade is already in progress.");
        }

        ApplicationDatabaseUpgradeJournal? journal = null;

        try
        {
            var configuration = databaseConfigurationService.GetConfiguration();
            if (!string.Equals(configuration.Provider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
            {
                return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Noop, "Database upgrade orchestration is only required for local SQLite.");
            }

            if (string.IsNullOrWhiteSpace(configuration.SqliteDatabasePath) || !File.Exists(configuration.SqliteDatabasePath))
            {
                return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Failed, "The local SQLite database does not exist.");
            }

            var integrity = await SqliteDatabaseBackupService.RunIntegrityCheckAsync(configuration.SqliteDatabasePath, cancellationToken);
            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Failed, "The local SQLite database failed integrity validation.");
            }

            var metadata = await metadataService.EnsureInitializedAsync(cancellationToken);
            if (metadata.DatabaseSchemaVersion > DatabaseSchemaInfo.CurrentSchemaVersion)
            {
                return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Rejected, "The database schema is newer than this application supports.");
            }

            if (metadata.DatabaseSchemaVersion == DatabaseSchemaInfo.CurrentSchemaVersion && !request.Force)
            {
                return new DatabaseUpgradeResult(
                    DatabaseUpgradeStatus.Noop,
                    "The local SQLite database schema is already current.",
                    FromSchemaVersion: metadata.DatabaseSchemaVersion,
                    TargetSchemaVersion: DatabaseSchemaInfo.CurrentSchemaVersion);
            }

            journal = new ApplicationDatabaseUpgradeJournal
            {
                StartedAtUtc = DateTime.UtcNow,
                FromSchemaVersion = metadata.DatabaseSchemaVersion,
                TargetSchemaVersion = DatabaseSchemaInfo.CurrentSchemaVersion,
                Status = DatabaseUpgradeJournalStatus.Started,
                ApplicationVersion = GetApplicationVersion(),
                InstallationId = metadata.InstallationId,
                CreatedBy = "system"
            };
            dbContext.ApplicationDatabaseUpgradeJournal.Add(journal);
            await dbContext.SaveChangesAsync(cancellationToken);

            var backup = await backupService.CreateBackupAsync(BackupReason.BeforeMigration, cancellationToken);
            if (backup.Status != BackupStatus.Succeeded)
            {
                await MarkFailedAsync(journal, "BackupFailed", "Verified pre-upgrade backup could not be created.", cancellationToken);
                return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Failed, "Upgrade stopped because the pre-upgrade backup failed.");
            }

            journal.PreUpgradeBackupId = backup.BackupId;
            journal.Status = DatabaseUpgradeJournalStatus.BackupCreated;
            await dbContext.SaveChangesAsync(cancellationToken);

            await dbContext.Database.MigrateAsync(cancellationToken);

            journal.Status = DatabaseUpgradeJournalStatus.MigrationsApplied;
            await dbContext.SaveChangesAsync(cancellationToken);

            var entity = await dbContext.ApplicationDatabaseMetadata.IgnoreQueryFilters().SingleAsync(cancellationToken);
            entity.DatabaseSchemaVersion = DatabaseSchemaInfo.CurrentSchemaVersion;
            entity.ApplicationVersion = GetApplicationVersion();
            entity.LastSuccessfulSchemaUpgradeAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            var afterIntegrity = await SqliteDatabaseBackupService.RunIntegrityCheckAsync(configuration.SqliteDatabasePath, cancellationToken);
            if (!string.Equals(afterIntegrity, "ok", StringComparison.OrdinalIgnoreCase))
            {
                await MarkFailedAsync(journal, "IntegrityFailed", "Post-upgrade integrity validation failed.", cancellationToken);
                return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Failed, "Post-upgrade database validation failed.", backup.BackupId);
            }

            journal.Status = DatabaseUpgradeJournalStatus.Completed;
            journal.CompletedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new DatabaseUpgradeResult(
                DatabaseUpgradeStatus.Completed,
                "Database upgrade completed successfully.",
                backup.BackupId,
                metadata.DatabaseSchemaVersion,
                DatabaseSchemaInfo.CurrentSchemaVersion);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            if (journal is not null)
            {
                await MarkFailedAsync(journal, "UpgradeFailed", Sanitize(exception.Message), CancellationToken.None);
            }

            return new DatabaseUpgradeResult(DatabaseUpgradeStatus.Failed, "Database upgrade failed. Check support diagnostics.");
        }
        finally
        {
            UpgradeLock.Release();
        }
    }

    private async Task MarkFailedAsync(
        ApplicationDatabaseUpgradeJournal journal,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        journal.Status = DatabaseUpgradeJournalStatus.Failed;
        journal.CompletedAtUtc = DateTime.UtcNow;
        journal.FailureCode = code;
        journal.FailureMessage = Sanitize(message);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Sanitize(string value)
        => value.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Length > 512
            ? value.Replace(Environment.NewLine, " ", StringComparison.Ordinal)[..512]
            : value.Replace(Environment.NewLine, " ", StringComparison.Ordinal);

    private static string GetApplicationVersion()
        => typeof(AppDbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
           ?? typeof(AppDbContext).Assembly.GetName().Version?.ToString()
           ?? "0.0.0";
}
