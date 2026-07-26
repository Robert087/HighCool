using ERP.Application.LocalData;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Infrastructure.LocalData;

public sealed class BackupManifestAuthenticationService(
    IBackupEncryptionKeyProvider encryptionKeyProvider) : IBackupManifestAuthenticationService
{
    public const int CurrentAuthenticationVersion = 1;
    private const string Algorithm = "HMAC-SHA256";
    private const string KeyDerivation = "HighCool.BackupManifestAuthentication.v1";

    public async Task<BackupManifest> SignAsync(
        BackupManifest manifest,
        string payloadObjectKey,
        string manifestObjectKey,
        CancellationToken cancellationToken)
    {
        var signature = await ComputeSignatureAsync(
            manifest with { Authentication = null },
            payloadObjectKey,
            manifestObjectKey,
            cancellationToken);

        return manifest with
        {
            Authentication = new BackupManifestAuthentication(
                CurrentAuthenticationVersion,
                Algorithm,
                KeyDerivation,
                signature)
        };
    }

    public async Task ValidateAsync(
        BackupManifest manifest,
        string payloadObjectKey,
        string manifestObjectKey,
        CancellationToken cancellationToken)
    {
        if (manifest.Authentication is null)
        {
            throw new InvalidOperationException("Cloud backup manifest is legacy and must be re-uploaded.");
        }

        if (manifest.Authentication.Version != CurrentAuthenticationVersion ||
            !string.Equals(manifest.Authentication.Algorithm, Algorithm, StringComparison.Ordinal) ||
            !string.Equals(manifest.Authentication.KeyDerivation, KeyDerivation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cloud backup manifest authentication version is not supported.");
        }

        var expected = await ComputeSignatureAsync(
            manifest with { Authentication = null },
            payloadObjectKey,
            manifestObjectKey,
            cancellationToken);
        byte[] expectedBytes;
        byte[] actualBytes;
        try
        {
            expectedBytes = Convert.FromHexString(expected);
            actualBytes = Convert.FromHexString(manifest.Authentication.Signature);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Cloud backup manifest authentication signature is invalid.", exception);
        }

        if (actualBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
        {
            throw new InvalidOperationException("Cloud backup manifest authentication failed.");
        }
    }

    private async Task<string> ComputeSignatureAsync(
        BackupManifest manifest,
        string payloadObjectKey,
        string manifestObjectKey,
        CancellationToken cancellationToken)
    {
        var key = await encryptionKeyProvider.GetOrCreateKeyAsync(cancellationToken);
        try
        {
            var derivedKey = HMACSHA256.HashData(
                key.KeyBytes,
                Encoding.UTF8.GetBytes(KeyDerivation));
            try
            {
                var canonical = BuildCanonicalManifest(manifest, payloadObjectKey, manifestObjectKey);
                var signature = HMACSHA256.HashData(derivedKey, Encoding.UTF8.GetBytes(canonical));
                return Convert.ToHexString(signature).ToLowerInvariant();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(derivedKey);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.KeyBytes);
        }
    }

    private static string BuildCanonicalManifest(
        BackupManifest manifest,
        string payloadObjectKey,
        string manifestObjectKey)
    {
        var fields = new[]
        {
            ("manifestVersion", manifest.ManifestVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("backupId", manifest.BackupId),
            ("installationId", manifest.InstallationId),
            ("applicationVersion", manifest.ApplicationVersion),
            ("databaseSchemaVersion", manifest.DatabaseSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("createdAtUtc", manifest.CreatedAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
            ("reason", manifest.Reason.ToString()),
            ("databaseFileName", manifest.DatabaseFileName),
            ("databaseSizeBytes", manifest.DatabaseSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("encryptedSizeBytes", (manifest.EncryptedSizeBytes ?? -1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("plainSha256", manifest.PlainSha256),
            ("encryptedSha256", manifest.EncryptedSha256),
            ("encryption.algorithm", manifest.Encryption.Algorithm),
            ("encryption.keyProtection", manifest.Encryption.KeyProtection),
            ("encryption.keyId", manifest.Encryption.KeyId),
            ("encryption.nonce", manifest.Encryption.Nonce),
            ("encryption.tag", manifest.Encryption.Tag),
            ("cloud.payloadObjectKey", payloadObjectKey),
            ("cloud.manifestObjectKey", manifestObjectKey)
        };

        return string.Join("\n", fields.Select(field => $"{field.Item1.Length}:{field.Item1}={field.Item2.Length}:{field.Item2}"));
    }
}
