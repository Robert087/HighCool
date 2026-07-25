using ERP.Application.LocalData;
using System.Security.Cryptography;

namespace ERP.Infrastructure.LocalData;

public sealed class DevelopmentFileBackupEncryptionKeyProvider(
    ILocalStoragePathService localStoragePathService) : IBackupEncryptionKeyProvider
{
    private const string KeyFileName = "highcool-local-backup.key";

    public async Task<BackupEncryptionKey> GetOrCreateKeyAsync(CancellationToken cancellationToken)
    {
        var keyDirectory = Path.Combine(localStoragePathService.DataDirectory, "Keys");
        Directory.CreateDirectory(keyDirectory);
        var keyPath = Path.Combine(keyDirectory, KeyFileName);

        if (!File.Exists(keyPath))
        {
            var key = RandomNumberGenerator.GetBytes(32);
            await File.WriteAllTextAsync(keyPath, Convert.ToBase64String(key), cancellationToken);
            TryRestrictFilePermissions(keyPath);
            CryptographicOperations.ZeroMemory(key);
        }

        var encodedKey = (await File.ReadAllTextAsync(keyPath, cancellationToken)).Trim();
        var keyBytes = Convert.FromBase64String(encodedKey);
        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException("Configured local backup encryption key is invalid.");
        }

        return new BackupEncryptionKey(keyBytes, ComputeKeyId(keyBytes));
    }

    private static string ComputeKeyId(byte[] keyBytes)
    {
        var hash = SHA256.HashData(keyBytes);
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private static void TryRestrictFilePermissions(string keyPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                keyPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Best-effort only for the Linux development provider. Windows desktop mode is expected
            // to use an OS-protected provider such as DPAPI in a later batch.
        }
    }
}
