using ERP.Application.LocalData;
using System.Text.Json;

namespace ERP.Infrastructure.LocalData;

public sealed class BackupCatalogService(
    IDatabaseConfigurationService databaseConfigurationService,
    ILocalStoragePathService localStoragePathService,
    SqliteDatabaseBackupService sqliteBackupService,
    BackupManifestService manifestService,
    IDatabaseRestoreService restoreService,
    Microsoft.Extensions.Options.IOptions<BackupRetentionOptions> retentionOptions) : IBackupCatalogService
{
    private const string VerificationExtension = ".verification.json";
    private const string RetentionSettingsFileName = "backup-retention-settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<BackupCenterSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var backups = await LoadBackupsAsync(cancellationToken);
        var configuration = databaseConfigurationService.GetConfiguration();
        var retention = await GetRetentionSettingsAsync(cancellationToken);
        var latest = backups.FirstOrDefault();
        var latestVerification = backups
            .Select(item => item.Verification)
            .Where(item => item?.VerifiedAtUtc is not null)
            .OrderByDescending(item => item!.VerifiedAtUtc)
            .FirstOrDefault();

        var reasons = new List<BackupHealthReasonDto>();
        var health = BackupHealthStatus.Unknown;

        if (!string.Equals(configuration.Provider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            health = BackupHealthStatus.Unknown;
            reasons.Add(new BackupHealthReasonDto("ProviderUnsupported", "Local backup status is available only for SQLite desktop databases."));
        }
        else if (backups.Count == 0)
        {
            health = BackupHealthStatus.Warning;
            reasons.Add(new BackupHealthReasonDto("NoBackups", "No backup has been created yet."));
        }
        else if (latest?.Verification?.Status == BackupIntegrityStatus.Failed)
        {
            health = BackupHealthStatus.Error;
            reasons.Add(new BackupHealthReasonDto("LatestVerificationFailed", "Latest backup could not be verified."));
        }
        else
        {
            health = BackupHealthStatus.Healthy;
            reasons.Add(new BackupHealthReasonDto("LatestBackupAvailable", "At least one local backup is available."));
        }

        var databaseFileName = configuration.SqliteDatabasePath is null
            ? null
            : Path.GetFileName(configuration.SqliteDatabasePath);
        var databaseSize = configuration.SqliteDatabasePath is not null && File.Exists(configuration.SqliteDatabasePath)
            ? new FileInfo(configuration.SqliteDatabasePath).Length
            : (long?)null;

        return new BackupCenterSummaryDto(
            health,
            reasons,
            latest?.Manifest.CreatedAtUtc,
            latestVerification?.VerifiedAtUtc,
            databaseFileName,
            databaseSize,
            backups.Count,
            backups.Sum(item => item.PayloadSizeBytes),
            latest?.Manifest.Encryption.Algorithm ?? "Not available",
            retention.Enabled,
            retention.Enabled ? "Healthy" : "Disabled",
            latest?.Manifest.ApplicationVersion,
            latest?.Manifest.DatabaseSchemaVersion,
            retention);
    }

    public async Task<IReadOnlyList<BackupListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var backups = await LoadBackupsAsync(cancellationToken);
        return backups.Select(item => new BackupListItemDto(
            item.Manifest.BackupId,
            item.Manifest.CreatedAtUtc,
            item.Manifest.Reason,
            item.PayloadExists ? BackupStatus.Succeeded : BackupStatus.Failed,
            item.PayloadSizeBytes,
            item.Manifest.ApplicationVersion,
            item.Manifest.DatabaseSchemaVersion,
            item.Verification?.Status ?? BackupIntegrityStatus.Unknown,
            item.Verification?.VerifiedAtUtc)).ToList();
    }

    public async Task<BackupDetailsDto> GetDetailsAsync(string backupId, CancellationToken cancellationToken)
    {
        var backup = await LoadBackupByIdAsync(backupId, cancellationToken);
        RestorePreflightResult? preflight = null;
        if (backup.PayloadExists && (backup.Verification?.Status ?? BackupIntegrityStatus.Unknown) != BackupIntegrityStatus.Failed)
        {
            preflight = await restoreService.ValidateAsync(new RestoreRequest(backup.Manifest.BackupId), cancellationToken);
        }

        return new BackupDetailsDto(
            backup.Manifest.BackupId,
            backup.Manifest.CreatedAtUtc,
            backup.Manifest.Reason,
            backup.PayloadExists ? BackupStatus.Succeeded : BackupStatus.Failed,
            backup.Manifest.ApplicationVersion,
            backup.Manifest.DatabaseSchemaVersion,
            backup.Manifest.ManifestVersion,
            backup.Manifest.Encryption.Algorithm,
            backup.PayloadSizeBytes,
            backup.Manifest.DatabaseSizeBytes,
            "None",
            backup.Manifest.EncryptedSha256,
            backup.Manifest.PlainSha256,
            backup.Verification?.Status ?? BackupIntegrityStatus.Unknown,
            backup.Verification?.VerifiedAtUtc,
            preflight?.Status,
            preflight?.Message,
            backup.Manifest.DatabaseFileName);
    }

    public async Task<BackupIntegrityVerificationResultDto> VerifyAsync(
        string backupId,
        CancellationToken cancellationToken)
    {
        var backup = await LoadBackupByIdAsync(backupId, cancellationToken);
        var verifiedAt = DateTime.UtcNow;
        var tempPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{backup.Manifest.BackupId}.verify.tmp");

        try
        {
            localStoragePathService.EnsureRequiredDirectories();
            await sqliteBackupService.DecryptBackupToTemporaryFileAsync(backup.Manifest, tempPath, cancellationToken);
            var integrity = await SqliteDatabaseBackupService.RunIntegrityCheckAsync(tempPath, cancellationToken);
            var success = string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase);
            var status = success ? BackupIntegrityStatus.Verified : BackupIntegrityStatus.Failed;
            var message = success ? "Backup integrity verified." : "Backup failed SQLite integrity verification.";

            await SaveVerificationAsync(backup, new BackupVerificationRecord(status, verifiedAt, message), cancellationToken);
            return new BackupIntegrityVerificationResultDto(backup.Manifest.BackupId, status, verifiedAt, message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException or System.Security.Cryptography.CryptographicException)
        {
            const string message = "Backup could not be verified safely.";
            await SaveVerificationAsync(backup, new BackupVerificationRecord(BackupIntegrityStatus.Failed, verifiedAt, message), cancellationToken);
            return new BackupIntegrityVerificationResultDto(backup.Manifest.BackupId, BackupIntegrityStatus.Failed, verifiedAt, message);
        }
        finally
        {
            DeleteTemporaryFiles(tempPath);
        }
    }

    public async Task<BackupRetentionSettingsDto> GetRetentionSettingsAsync(CancellationToken cancellationToken)
    {
        localStoragePathService.EnsureRequiredDirectories();
        var path = GetRetentionSettingsPath();
        if (!File.Exists(path))
        {
            return ToDto(retentionOptions.Value);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<BackupRetentionSettingsDto>(stream, JsonOptions, cancellationToken);
            return Normalize(settings ?? ToDto(retentionOptions.Value));
        }
        catch (JsonException)
        {
            return ToDto(retentionOptions.Value);
        }
    }

    public async Task<BackupRetentionSettingsDto> SaveRetentionSettingsAsync(
        BackupRetentionSettingsDto settings,
        CancellationToken cancellationToken)
    {
        localStoragePathService.EnsureRequiredDirectories();
        var normalized = Normalize(settings);
        await File.WriteAllTextAsync(GetRetentionSettingsPath(), JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);
        return normalized;
    }

    private async Task<IReadOnlyList<BackupCatalogEntry>> LoadBackupsAsync(CancellationToken cancellationToken)
    {
        localStoragePathService.EnsureRequiredDirectories();
        var backups = new List<BackupCatalogEntry>();

        foreach (var manifestPath in manifestService.EnumerateManifestPaths(localStoragePathService.BackupDirectory))
        {
            try
            {
                var manifest = await manifestService.ReadAndValidateAsync(manifestPath, cancellationToken);
                var payloadPath = Path.Combine(localStoragePathService.BackupDirectory, manifest.DatabaseFileName);
                if (!IsSameOrChildPath(localStoragePathService.BackupDirectory, payloadPath))
                {
                    continue;
                }

                var verification = await ReadVerificationAsync(manifestPath, cancellationToken);
                backups.Add(new BackupCatalogEntry(
                    manifest,
                    manifestPath,
                    payloadPath,
                    File.Exists(payloadPath),
                    File.Exists(payloadPath) ? new FileInfo(payloadPath).Length : 0,
                    verification));
            }
            catch (InvalidOperationException)
            {
                // Invalid manifests are preserved by retention and omitted from the user-facing catalog.
            }
        }

        return backups
            .OrderByDescending(item => item.Manifest.CreatedAtUtc)
            .ThenBy(item => item.Manifest.BackupId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<BackupCatalogEntry> LoadBackupByIdAsync(string backupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backupId) || backupId.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException("Backup ID is invalid.");
        }

        var backup = (await LoadBackupsAsync(cancellationToken))
            .SingleOrDefault(item => string.Equals(item.Manifest.BackupId, backupId, StringComparison.OrdinalIgnoreCase));

        return backup ?? throw new InvalidOperationException("Backup was not found.");
    }

    private async Task<BackupVerificationRecord?> ReadVerificationAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        var path = GetVerificationPath(manifestPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<BackupVerificationRecord>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task SaveVerificationAsync(
        BackupCatalogEntry backup,
        BackupVerificationRecord record,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            GetVerificationPath(backup.ManifestPath),
            JsonSerializer.Serialize(record, JsonOptions),
            cancellationToken);
    }

    private string GetRetentionSettingsPath()
        => Path.Combine(localStoragePathService.DataDirectory, RetentionSettingsFileName);

    private static string GetVerificationPath(string manifestPath)
        => $"{manifestPath}{VerificationExtension}";

    private static BackupRetentionSettingsDto ToDto(BackupRetentionOptions options)
        => Normalize(new BackupRetentionSettingsDto(
            options.Enabled,
            options.ManualCount,
            options.ScheduledCount,
            options.BeforeMigrationCount,
            options.BeforeRestoreCount,
            options.BeforeApplicationUpdateCount,
            options.MinimumAgeHoursBeforeDeletion));

    private static BackupRetentionSettingsDto Normalize(BackupRetentionSettingsDto settings)
        => settings with
        {
            ManualCount = Math.Clamp(settings.ManualCount, 1, 365),
            ScheduledCount = Math.Clamp(settings.ScheduledCount, 1, 365),
            BeforeMigrationCount = Math.Clamp(settings.BeforeMigrationCount, 1, 365),
            BeforeRestoreCount = Math.Clamp(settings.BeforeRestoreCount, 1, 365),
            BeforeApplicationUpdateCount = Math.Clamp(settings.BeforeApplicationUpdateCount, 1, 365),
            MinimumAgeHoursBeforeDeletion = Math.Clamp(settings.MinimumAgeHoursBeforeDeletion, 0, 8760)
        };

    private static bool IsSameOrChildPath(string parentPath, string candidatePath)
    {
        var parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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

    private sealed record BackupCatalogEntry(
        BackupManifest Manifest,
        string ManifestPath,
        string PayloadPath,
        bool PayloadExists,
        long PayloadSizeBytes,
        BackupVerificationRecord? Verification);

    private sealed record BackupVerificationRecord(
        BackupIntegrityStatus Status,
        DateTime VerifiedAtUtc,
        string Message);
}
