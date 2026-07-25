using ERP.Application.LocalData;
using ERP.Domain.System;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.LocalData;

public sealed class DatabaseRestoreService(
    AppDbContext dbContext,
    IDatabaseConfigurationService databaseConfigurationService,
    ILocalStoragePathService localStoragePathService,
    IApplicationDatabaseMetadataService metadataService,
    IDatabaseBackupService backupService,
    SqliteDatabaseBackupService sqliteBackupService,
    BackupManifestService manifestService,
    IDatabaseHealthService healthService) : IDatabaseRestoreService
{
    public const string RequiredConfirmation = "RESTORE_LOCAL_DATABASE";
    private static readonly SemaphoreSlim RestoreLock = new(1, 1);

    public async Task<RestorePreflightResult> ValidateAsync(
        RestoreRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await LoadManifestByBackupIdAsync(request.BackupId, cancellationToken);
            var metadata = await metadataService.EnsureInitializedAsync(cancellationToken);
            if (!string.Equals(manifest.InstallationId, metadata.InstallationId, StringComparison.Ordinal))
            {
                return new RestorePreflightResult(RestorePreflightStatus.WrongInstallation, "Backup belongs to a different installation.", request.BackupId);
            }

            if (manifest.DatabaseSchemaVersion > DatabaseSchemaInfo.CurrentSchemaVersion)
            {
                return new RestorePreflightResult(RestorePreflightStatus.NewerSchema, "Backup schema is newer than this application supports.", request.BackupId, manifest.DatabaseSchemaVersion);
            }

            var tempPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{manifest.BackupId}.restore-preflight.tmp");
            try
            {
                localStoragePathService.EnsureRequiredDirectories();
                await sqliteBackupService.DecryptBackupToTemporaryFileAsync(manifest, tempPath, cancellationToken);
                var validation = await ValidatePlainDatabaseAsync(tempPath, manifest.DatabaseSchemaVersion, cancellationToken);
                return validation == RestorePreflightStatus.Valid
                    ? new RestorePreflightResult(RestorePreflightStatus.Valid, "Backup is valid for restore.", request.BackupId, manifest.DatabaseSchemaVersion)
                    : new RestorePreflightResult(validation, "Backup failed restore preflight validation.", request.BackupId, manifest.DatabaseSchemaVersion);
            }
            finally
            {
                DeleteTemporaryFiles(tempPath);
            }
        }
        catch (InvalidOperationException exception)
        {
            var status = exception.Message.Contains("checksum", StringComparison.OrdinalIgnoreCase)
                ? RestorePreflightStatus.ChecksumMismatch
                : exception.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase)
                    ? RestorePreflightStatus.DecryptionFailed
                    : RestorePreflightStatus.ManifestInvalid;
            return new RestorePreflightResult(status, Sanitize(exception.Message), request.BackupId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or System.Security.Cryptography.CryptographicException)
        {
            return new RestorePreflightResult(RestorePreflightStatus.CorruptDatabase, "Backup could not be validated safely.", request.BackupId);
        }
    }

    public async Task<RestoreResult> RestoreAsync(
        RestoreRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Confirmation, RequiredConfirmation, StringComparison.Ordinal))
        {
            return new RestoreResult(RestoreStatus.Rejected, "Restore confirmation value is required.", request.BackupId);
        }

        if (!await RestoreLock.WaitAsync(0, cancellationToken))
        {
            return new RestoreResult(RestoreStatus.Rejected, "A database restore is already in progress.", request.BackupId);
        }

        ApplicationDatabaseRestoreJournal? journal = null;
        string? restoreTempPath = null;
        string? rollbackPath = null;

        try
        {
            var preflight = await ValidateAsync(request, cancellationToken);
            if (preflight.Status != RestorePreflightStatus.Valid)
            {
                return new RestoreResult(RestoreStatus.Rejected, preflight.Message, request.BackupId);
            }

            var configuration = databaseConfigurationService.GetConfiguration();
            if (string.IsNullOrWhiteSpace(configuration.SqliteDatabasePath))
            {
                return new RestoreResult(RestoreStatus.Failed, "Restore is available only for local SQLite.", request.BackupId);
            }

            var originalMetadata = await metadataService.EnsureInitializedAsync(cancellationToken);
            journal = new ApplicationDatabaseRestoreJournal
            {
                StartedAtUtc = DateTime.UtcNow,
                SelectedBackupId = request.BackupId,
                OriginalSchemaVersion = originalMetadata.DatabaseSchemaVersion,
                Status = DatabaseRestoreJournalStatus.Started,
                ApplicationVersion = originalMetadata.ApplicationVersion,
                InstallationId = originalMetadata.InstallationId,
                CreatedBy = "system"
            };
            dbContext.ApplicationDatabaseRestoreJournal.Add(journal);
            await dbContext.SaveChangesAsync(cancellationToken);

            var safetyBackup = await backupService.CreateBackupAsync(BackupReason.BeforeRestore, cancellationToken);
            if (safetyBackup.Status != BackupStatus.Succeeded)
            {
                await MarkFailedAsync(journal, "SafetyBackupFailed", "BeforeRestore safety backup failed.", cancellationToken);
                return new RestoreResult(RestoreStatus.Failed, "Restore stopped because safety backup failed.", request.BackupId);
            }

            journal.SafetyBackupId = safetyBackup.BackupId;
            journal.Status = DatabaseRestoreJournalStatus.SafetyBackupCreated;
            await dbContext.SaveChangesAsync(cancellationToken);

            var selectedManifest = await LoadManifestByBackupIdAsync(request.BackupId, cancellationToken);
            restoreTempPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{request.BackupId}.restore.tmp");
            await sqliteBackupService.DecryptBackupToTemporaryFileAsync(selectedManifest, restoreTempPath, cancellationToken);
            var secondValidation = await ValidatePlainDatabaseAsync(restoreTempPath, selectedManifest.DatabaseSchemaVersion, cancellationToken);
            if (secondValidation != RestorePreflightStatus.Valid)
            {
                await MarkFailedAsync(journal, "SelectedBackupInvalid", "Selected backup failed second validation.", cancellationToken);
                return new RestoreResult(RestoreStatus.Failed, "Selected backup failed restore validation.", request.BackupId, safetyBackup.BackupId);
            }

            await dbContext.Database.CloseConnectionAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            rollbackPath = $"{configuration.SqliteDatabasePath}.rollback-{Guid.NewGuid():N}";
            File.Move(configuration.SqliteDatabasePath, rollbackPath);
            File.Move(restoreTempPath, configuration.SqliteDatabasePath);

            journal.Status = DatabaseRestoreJournalStatus.DatabaseReplaced;
            journal.RestoredSchemaVersion = selectedManifest.DatabaseSchemaVersion;
            await UpsertRestoreJournalSnapshotAsync(configuration.SqliteDatabasePath, journal, cancellationToken);

            var health = await healthService.CheckAsync(requireWritable: true, cancellationToken);
            if (health.Status != DatabaseHealthStatus.Healthy)
            {
                await RollbackAsync(configuration.SqliteDatabasePath, rollbackPath, cancellationToken);
                journal.Status = DatabaseRestoreJournalStatus.RolledBack;
                await MarkFailedAsync(journal, "PostRestoreHealthFailed", "Post-restore health validation failed and rollback was attempted.", cancellationToken);
                return new RestoreResult(RestoreStatus.Failed, "Post-restore health validation failed; rollback was attempted.", request.BackupId, safetyBackup.BackupId);
            }

            journal.Status = DatabaseRestoreJournalStatus.Completed;
            journal.CompletedAtUtc = DateTime.UtcNow;
            await UpsertRestoreJournalSnapshotAsync(configuration.SqliteDatabasePath, journal, cancellationToken);

            DeleteIfExists(rollbackPath);

            return new RestoreResult(RestoreStatus.Completed, "Database restore completed successfully.", request.BackupId, safetyBackup.BackupId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            if (journal is not null)
            {
                await MarkFailedAsync(journal, "RestoreFailed", Sanitize(exception.Message), CancellationToken.None);
            }

            return new RestoreResult(RestoreStatus.Failed, "Database restore failed. Check support diagnostics.", request.BackupId);
        }
        finally
        {
            DeleteTemporaryFiles(restoreTempPath);
            RestoreLock.Release();
        }
    }

    private async Task<BackupManifest> LoadManifestByBackupIdAsync(string backupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backupId) || backupId.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new InvalidOperationException("Backup ID is invalid.");
        }

        localStoragePathService.EnsureRequiredDirectories();
        foreach (var manifestPath in manifestService.EnumerateManifestPaths(localStoragePathService.BackupDirectory))
        {
            var manifest = await manifestService.ReadAndValidateAsync(manifestPath, cancellationToken);
            if (string.Equals(manifest.BackupId, backupId, StringComparison.OrdinalIgnoreCase))
            {
                return manifest;
            }
        }

        throw new InvalidOperationException("Backup was not found.");
    }

    private static async Task<RestorePreflightStatus> ValidatePlainDatabaseAsync(
        string databasePath,
        int schemaVersion,
        CancellationToken cancellationToken)
    {
        var integrity = await SqliteDatabaseBackupService.RunIntegrityCheckAsync(databasePath, cancellationToken);
        if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return RestorePreflightStatus.CorruptDatabase;
        }

        if (schemaVersion > DatabaseSchemaInfo.CurrentSchemaVersion)
        {
            return RestorePreflightStatus.NewerSchema;
        }

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        foreach (var tableName in new[] { "application_database_metadata", "Organizations", "UserAccounts", "Roles" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", tableName);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                return RestorePreflightStatus.UnsupportedSchema;
            }
        }

        return RestorePreflightStatus.Valid;
    }

    private async Task MarkFailedAsync(
        ApplicationDatabaseRestoreJournal journal,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        journal.Status = DatabaseRestoreJournalStatus.Failed;
        journal.CompletedAtUtc = DateTime.UtcNow;
        journal.FailureCode = code;
        journal.FailureMessage = Sanitize(message);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertRestoreJournalSnapshotAsync(
        string databasePath,
        ApplicationDatabaseRestoreJournal journal,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO application_database_restore_journal (
                Id,
                started_at_utc,
                completed_at_utc,
                selected_backup_id,
                safety_backup_id,
                original_schema_version,
                restored_schema_version,
                status,
                application_version,
                installation_id,
                failure_code,
                failure_message,
                created_at,
                created_by,
                updated_at,
                updated_by
            )
            VALUES (
                $id,
                $startedAtUtc,
                $completedAtUtc,
                $selectedBackupId,
                $safetyBackupId,
                $originalSchemaVersion,
                $restoredSchemaVersion,
                $status,
                $applicationVersion,
                $installationId,
                $failureCode,
                $failureMessage,
                $createdAt,
                $createdBy,
                $updatedAt,
                $updatedBy
            )
            ON CONFLICT(Id) DO UPDATE SET
                completed_at_utc = excluded.completed_at_utc,
                safety_backup_id = excluded.safety_backup_id,
                original_schema_version = excluded.original_schema_version,
                restored_schema_version = excluded.restored_schema_version,
                status = excluded.status,
                failure_code = excluded.failure_code,
                failure_message = excluded.failure_message,
                updated_at = excluded.updated_at,
                updated_by = excluded.updated_by;
            """;

        command.Parameters.AddWithValue("$id", journal.Id.ToString());
        command.Parameters.AddWithValue("$startedAtUtc", journal.StartedAtUtc);
        command.Parameters.AddWithValue("$completedAtUtc", (object?)journal.CompletedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("$selectedBackupId", journal.SelectedBackupId);
        command.Parameters.AddWithValue("$safetyBackupId", (object?)journal.SafetyBackupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$originalSchemaVersion", (object?)journal.OriginalSchemaVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$restoredSchemaVersion", (object?)journal.RestoredSchemaVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", journal.Status.ToString());
        command.Parameters.AddWithValue("$applicationVersion", journal.ApplicationVersion);
        command.Parameters.AddWithValue("$installationId", journal.InstallationId);
        command.Parameters.AddWithValue("$failureCode", (object?)journal.FailureCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$failureMessage", (object?)journal.FailureMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", journal.CreatedAt == default ? utcNow : journal.CreatedAt);
        command.Parameters.AddWithValue("$createdBy", string.IsNullOrWhiteSpace(journal.CreatedBy) ? "system" : journal.CreatedBy);
        command.Parameters.AddWithValue("$updatedAt", utcNow);
        command.Parameters.AddWithValue("$updatedBy", "system");

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RollbackAsync(string livePath, string rollbackPath, CancellationToken cancellationToken)
    {
        await Task.Yield();
        DeleteIfExists(livePath);
        if (File.Exists(rollbackPath))
        {
            File.Move(rollbackPath, livePath);
        }
    }

    private static string Sanitize(string value)
    {
        var sanitized = value.Replace(Environment.NewLine, " ", StringComparison.Ordinal);
        return sanitized.Length > 512 ? sanitized[..512] : sanitized;
    }

    private static void DeleteTemporaryFiles(string? filePath)
    {
        DeleteIfExists(filePath);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            DeleteIfExists($"{filePath}-wal");
            DeleteIfExists($"{filePath}-shm");
        }
    }

    private static void DeleteIfExists(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
