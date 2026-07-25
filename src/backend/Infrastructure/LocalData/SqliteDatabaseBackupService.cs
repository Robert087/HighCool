using ERP.Application.LocalData;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace ERP.Infrastructure.LocalData;

public sealed class SqliteDatabaseBackupService(
    IDatabaseConfigurationService databaseConfigurationService,
    ILocalStoragePathService localStoragePathService,
    IApplicationDatabaseMetadataService metadataService,
    IBackupEncryptionKeyProvider encryptionKeyProvider,
    BackupManifestService manifestService,
    ILocalDatabaseOperationCoordinator operationCoordinator) : IDatabaseBackupService
{
    public async Task<BackupResult> CreateBackupAsync(
        BackupReason reason,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow;
        var backupId = Guid.NewGuid().ToString("N");
        string? plainTempPath = null;
        string? encryptedTempPath = null;
        string? finalEncryptedPath = null;
        string? finalManifestPath = null;

        if (cancellationToken.IsCancellationRequested)
        {
            return new BackupResult(BackupStatus.Canceled, backupId, timestamp, 0, null, reason, "Backup was canceled before completion.");
        }

        var lease = await operationCoordinator.TryAcquireExclusiveAsync(
            LocalDatabaseOperationKind.Backup,
            null,
            cancellationToken);
        if (lease is null)
        {
            return Failure(backupId, timestamp, reason, "A local database operation is already in progress.");
        }

        await using (lease)
        {
            try
            {
                var databaseConfiguration = databaseConfigurationService.GetConfiguration();
                if (!string.Equals(databaseConfiguration.Provider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(databaseConfiguration.SqliteDatabasePath))
                {
                    return Failure(backupId, timestamp, reason, "Local SQLite backup is available only when Database:Provider is Sqlite.");
                }

                if (!File.Exists(databaseConfiguration.SqliteDatabasePath))
                {
                    return Failure(backupId, timestamp, reason, "The local SQLite database does not exist.");
                }

                localStoragePathService.EnsureRequiredDirectories();
                EnsureBackupDirectoryIsOutsideDataDirectory();

                cancellationToken.ThrowIfCancellationRequested();

                finalEncryptedPath = CreateUniqueFinalPath(timestamp, reason, backupId);
                finalManifestPath = manifestService.GetManifestPathForBackupFile(finalEncryptedPath);
                plainTempPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{Path.GetFileName(finalEncryptedPath)}.{backupId}.plain.tmp");
                encryptedTempPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{Path.GetFileName(finalEncryptedPath)}.{backupId}.enc.tmp");

                await BackupSqliteDatabaseAsync(databaseConfiguration.ConnectionString, plainTempPath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var integrityCheck = await RunIntegrityCheckAsync(plainTempPath, cancellationToken);
                if (!string.Equals(integrityCheck, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteTemporaryBackupFiles(plainTempPath);
                    DeleteTemporaryBackupFiles(encryptedTempPath);
                    return Failure(backupId, timestamp, reason, "The created SQLite backup failed integrity validation.");
                }

                var metadata = await metadataService.EnsureInitializedAsync(cancellationToken);
                var plainChecksum = await CalculateChecksumAsync(plainTempPath, cancellationToken);
                var plainSizeBytes = new FileInfo(plainTempPath).Length;
                var encryption = await EncryptAsync(plainTempPath, encryptedTempPath, cancellationToken);
                var encryptedChecksum = await CalculateChecksumAsync(encryptedTempPath, cancellationToken);

                var manifest = new BackupManifest(
                    BackupManifestService.CurrentManifestVersion,
                    backupId,
                    metadata.InstallationId,
                    metadata.ApplicationVersion,
                    metadata.DatabaseSchemaVersion,
                    timestamp,
                    reason,
                    Path.GetFileName(finalEncryptedPath),
                    plainSizeBytes,
                    plainChecksum,
                    encryptedChecksum,
                    encryption);

                await manifestService.WriteAsync(finalManifestPath, manifest, cancellationToken);

                File.Move(encryptedTempPath, finalEncryptedPath, overwrite: false);
                DeleteTemporaryBackupFiles(plainTempPath);
                DeleteTemporaryBackupFiles(encryptedTempPath);

                return new BackupResult(
                    BackupStatus.Succeeded,
                    backupId,
                    timestamp,
                    plainSizeBytes,
                    encryptedChecksum,
                    reason,
                    "Backup created successfully.",
                    Path.GetFileName(finalEncryptedPath),
                    Path.GetFileName(finalManifestPath));
            }
            catch (OperationCanceledException)
            {
                DeleteTemporaryBackupFiles(plainTempPath);
                DeleteTemporaryBackupFiles(encryptedTempPath);
                DeleteIfExists(finalManifestPath);
                return new BackupResult(BackupStatus.Canceled, backupId, timestamp, 0, null, reason, "Backup was canceled before completion.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException or CryptographicException)
            {
                DeleteTemporaryBackupFiles(plainTempPath);
                DeleteTemporaryBackupFiles(encryptedTempPath);
                DeleteIfExists(finalManifestPath);
                DeleteIfExists(finalEncryptedPath);
                return Failure(backupId, timestamp, reason, "Backup failed. Check local storage configuration and application logs.");
            }
        }
    }

    public async Task<string> DecryptBackupToTemporaryFileAsync(
        BackupManifest manifest,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var encryptedPath = Path.Combine(localStoragePathService.BackupDirectory, manifest.DatabaseFileName);
        var encryptedChecksum = await CalculateChecksumAsync(encryptedPath, cancellationToken);
        if (!string.Equals(encryptedChecksum, manifest.EncryptedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup checksum does not match the manifest.");
        }

        var key = await encryptionKeyProvider.GetOrCreateKeyAsync(cancellationToken);
        var nonce = Convert.FromBase64String(manifest.Encryption.Nonce);
        var tag = Convert.FromBase64String(manifest.Encryption.Tag);
        var cipherBytes = await File.ReadAllBytesAsync(encryptedPath, cancellationToken);
        var plainBytes = new byte[cipherBytes.Length];

        try
        {
            using var aes = new AesGcm(key.KeyBytes, tagSizeInBytes: 16);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            await File.WriteAllBytesAsync(destinationPath, plainBytes, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(key.KeyBytes);
        }

        var plainChecksum = await CalculateChecksumAsync(destinationPath, cancellationToken);
        if (!string.Equals(plainChecksum, manifest.PlainSha256, StringComparison.OrdinalIgnoreCase))
        {
            DeleteTemporaryBackupFiles(destinationPath);
            throw new InvalidOperationException("Decrypted backup checksum does not match the manifest.");
        }

        return destinationPath;
    }

    private static BackupResult Failure(string backupId, DateTime timestamp, BackupReason reason, string message)
        => new(BackupStatus.Failed, backupId, timestamp, 0, null, reason, message);

    private async Task BackupSqliteDatabaseAsync(
        string sourceConnectionString,
        string tempPath,
        CancellationToken cancellationToken)
    {
        await using var source = new SqliteConnection(sourceConnectionString);
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = tempPath
        }.ToString());

        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private async Task<BackupEncryptionManifest> EncryptAsync(
        string plainPath,
        string encryptedPath,
        CancellationToken cancellationToken)
    {
        var key = await encryptionKeyProvider.GetOrCreateKeyAsync(cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = await File.ReadAllBytesAsync(plainPath, cancellationToken);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        try
        {
            using var aes = new AesGcm(key.KeyBytes, tagSizeInBytes: 16);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            await File.WriteAllBytesAsync(encryptedPath, cipherBytes, cancellationToken);

            return new BackupEncryptionManifest(
                "AES-256-GCM",
                "LinuxDevelopmentFileKey-NotProductionDPAPI",
                key.KeyId,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.KeyBytes);
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(cipherBytes);
        }
    }

    public static async Task<string> RunIntegrityCheckAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());

        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
    }

    public static async Task<string> CalculateChecksumAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string CreateUniqueFinalPath(DateTime timestamp, BackupReason reason, string backupId)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = attempt == 0 ? backupId[..8] : $"{backupId[..8]}-{attempt}";
            var fileName = $"HighCool_{timestamp:yyyyMMdd_HHmmss}_{reason}_{suffix}.db.enc";
            var path = Path.Combine(localStoragePathService.BackupDirectory, fileName);

            if (!File.Exists(path) && !File.Exists(manifestService.GetManifestPathForBackupFile(path)))
            {
                return path;
            }
        }

        throw new InvalidOperationException("Could not create a unique local backup file name.");
    }

    private void EnsureBackupDirectoryIsOutsideDataDirectory()
    {
        var dataDirectory = NormalizeDirectory(localStoragePathService.DataDirectory);
        var backupDirectory = NormalizeDirectory(localStoragePathService.BackupDirectory);

        if (string.Equals(dataDirectory, backupDirectory, StringComparison.OrdinalIgnoreCase) ||
            backupDirectory.StartsWith(dataDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("LocalStorage:BackupDirectory must be outside LocalStorage:DataDirectory.");
        }
    }

    private static string NormalizeDirectory(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void DeleteTemporaryBackupFiles(string? filePath)
    {
        DeleteIfExists(filePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        DeleteIfExists($"{filePath}-wal");
        DeleteIfExists($"{filePath}-shm");
    }

    private static void DeleteIfExists(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
