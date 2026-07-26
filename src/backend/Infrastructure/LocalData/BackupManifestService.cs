using ERP.Application.LocalData;
using System.Text.Json;

namespace ERP.Infrastructure.LocalData;

public sealed class BackupManifestService
{
    public const int CurrentManifestVersion = 1;
    public const string ManifestExtension = ".manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task WriteAsync(
        string manifestPath,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        ValidateSafeFileName(manifest.DatabaseFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }

    public async Task<BackupManifest> ReadAndValidateAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("Backup manifest was not found.");
        }

        BackupManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Backup manifest is invalid JSON.", exception);
        }

        if (manifest is null)
        {
            throw new InvalidOperationException("Backup manifest is empty.");
        }

        if (manifest.ManifestVersion != CurrentManifestVersion)
        {
            throw new InvalidOperationException("Backup manifest version is not supported.");
        }

        if (string.IsNullOrWhiteSpace(manifest.BackupId) ||
            string.IsNullOrWhiteSpace(manifest.InstallationId) ||
            string.IsNullOrWhiteSpace(manifest.ApplicationVersion) ||
            string.IsNullOrWhiteSpace(manifest.DatabaseFileName) ||
            string.IsNullOrWhiteSpace(manifest.PlainSha256) ||
            string.IsNullOrWhiteSpace(manifest.EncryptedSha256) ||
            string.IsNullOrWhiteSpace(manifest.Encryption.Algorithm) ||
            string.IsNullOrWhiteSpace(manifest.Encryption.Nonce) ||
            string.IsNullOrWhiteSpace(manifest.Encryption.Tag))
        {
            throw new InvalidOperationException("Backup manifest is missing required fields.");
        }

        ValidateSafeFileName(manifest.DatabaseFileName);

        if (!string.Equals(manifest.Encryption.Algorithm, "AES-256-GCM", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Backup encryption algorithm is not supported.");
        }

        if (manifest.Authentication is not null && (!manifest.EncryptedSizeBytes.HasValue || manifest.EncryptedSizeBytes.Value <= 0))
        {
            throw new InvalidOperationException("Authenticated backup manifest is missing encrypted size.");
        }

        var expectedBackupIdFragment = manifest.BackupId[..Math.Min(8, manifest.BackupId.Length)];
        if (!Path.GetFileName(manifestPath).Contains(expectedBackupIdFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup manifest and backup ID do not match.");
        }

        return manifest;
    }

    public string GetManifestPathForBackupFile(string backupFilePath)
        => Path.Combine(
            Path.GetDirectoryName(backupFilePath)!,
            $"{Path.GetFileNameWithoutExtension(backupFilePath)}{ManifestExtension}");

    public IEnumerable<string> EnumerateManifestPaths(string backupDirectory)
        => Directory.Exists(backupDirectory)
            ? Directory.EnumerateFiles(backupDirectory, $"*{ManifestExtension}")
            : [];

    private static void ValidateSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName != Path.GetFileName(fileName) ||
            fileName.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(fileName))
        {
            throw new InvalidOperationException("Backup manifest contains an unsafe file name.");
        }
    }
}
