using ERP.Application.LocalData;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ERP.Infrastructure.LocalData;

public sealed class CloudBackupConfigurationStore(
    ILocalStoragePathService localStoragePathService,
    IBackupEncryptionKeyProvider encryptionKeyProvider) : ICloudBackupConfigurationStore
{
    private const string FileName = "cloud-backup-settings.json";
    private static readonly Regex R2EndpointHostPattern =
        new("^[a-f0-9]{32}\\.r2\\.cloudflarestorage\\.com$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BucketNamePattern =
        new("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<CloudBackupConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var stored = await ReadStoredAsync(cancellationToken);
        var credentials = await TryDecryptAsync(stored.Credentials, cancellationToken);
        return ToDto(stored, credentials.Credentials, credentials.Unreadable);
    }

    public async Task<CloudBackupConfigurationDto> SaveConfigurationAsync(
        CloudBackupConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var current = await ReadStoredAsync(cancellationToken);
        var currentCredentials = await TryDecryptAsync(current.Credentials, cancellationToken);
        var credentials = NormalizeCredentials(request, currentCredentials);
        var normalized = new StoredCloudBackupConfiguration
        {
            Enabled = request.Enabled,
            AutoUploadAfterBackup = request.AutoUploadAfterBackup,
            BucketName = NormalizeBucketName(request.BucketName),
            Endpoint = await NormalizeEndpointAsync(request.Endpoint, cancellationToken),
            Prefix = NormalizePrefix(request.Prefix),
            RetentionCount = Math.Clamp(request.RetentionCount, 1, 365),
            ConnectionTimeoutSeconds = Math.Clamp(request.ConnectionTimeoutSeconds, 3, 120),
            RetryCount = Math.Clamp(request.RetryCount, 1, 10),
            Credentials = await EncryptAsync(credentials, cancellationToken)
        };

        localStoragePathService.EnsureRequiredDirectories();
        await File.WriteAllTextAsync(GetPath(), JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);
        TryRestrictFilePermissions(GetPath());
        return ToDto(normalized, credentials, false);
    }

    public async Task<CloudBackupSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var stored = await ReadStoredAsync(cancellationToken);
        var credentials = await TryDecryptAsync(stored.Credentials, cancellationToken);
        if (credentials.Unreadable)
        {
            throw new CloudBackupConnectionException(
                CloudBackupConnectionFailureCategory.CredentialsUnreadable,
                CloudBackupConnectionTestStage.Credentials,
                "The saved cloud backup credentials could not be decrypted. Replace the credentials and try again.",
                cleanupSucceeded: false);
        }

        return new CloudBackupSettings(
            stored.Enabled,
            stored.AutoUploadAfterBackup,
            stored.BucketName,
            stored.Endpoint,
            credentials.Credentials.AccessKey,
            credentials.Credentials.SecretKey,
            stored.Prefix,
            stored.RetentionCount,
            stored.ConnectionTimeoutSeconds,
            stored.RetryCount);
    }

    private async Task<StoredCloudBackupConfiguration> ReadStoredAsync(CancellationToken cancellationToken)
    {
        localStoragePathService.EnsureRequiredDirectories();
        var path = GetPath();
        if (!File.Exists(path))
        {
            return new StoredCloudBackupConfiguration();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<StoredCloudBackupConfiguration>(
                stream,
                JsonOptions,
                cancellationToken) ?? new StoredCloudBackupConfiguration();
        }
        catch (JsonException)
        {
            return new StoredCloudBackupConfiguration();
        }
    }

    private async Task<EncryptedCloudBackupCredentials?> EncryptAsync(
        CloudBackupCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credentials.AccessKey) && string.IsNullOrWhiteSpace(credentials.SecretKey))
        {
            return null;
        }

        var key = await encryptionKeyProvider.GetOrCreateKeyAsync(cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials, JsonOptions));
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        try
        {
            using var aes = new AesGcm(key.KeyBytes, tagSizeInBytes: 16);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            return new EncryptedCloudBackupCredentials(
                "AES-256-GCM",
                key.KeyId,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(cipherBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.KeyBytes);
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(cipherBytes);
        }
    }

    private async Task<CloudBackupCredentialReadResult> TryDecryptAsync(
        EncryptedCloudBackupCredentials? encrypted,
        CancellationToken cancellationToken)
    {
        if (encrypted is null || string.IsNullOrWhiteSpace(encrypted.CipherText))
        {
            return new CloudBackupCredentialReadResult(new CloudBackupCredentials("", ""), false);
        }

        BackupEncryptionKey key;
        byte[] cipherBytes;
        byte[] plainBytes;
        try
        {
            key = await encryptionKeyProvider.GetOrCreateKeyAsync(cancellationToken);
            cipherBytes = Convert.FromBase64String(encrypted.CipherText);
            plainBytes = new byte[cipherBytes.Length];
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or IOException or UnauthorizedAccessException)
        {
            return new CloudBackupCredentialReadResult(new CloudBackupCredentials("", ""), true);
        }

        try
        {
            using var aes = new AesGcm(key.KeyBytes, tagSizeInBytes: 16);
            aes.Decrypt(
                Convert.FromBase64String(encrypted.Nonce),
                cipherBytes,
                Convert.FromBase64String(encrypted.Tag),
                plainBytes);
            var credentials = JsonSerializer.Deserialize<CloudBackupCredentials>(
                Encoding.UTF8.GetString(plainBytes),
                JsonOptions);
            return credentials is null
                ? new CloudBackupCredentialReadResult(new CloudBackupCredentials("", ""), true)
                : new CloudBackupCredentialReadResult(credentials, false);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or CryptographicException)
        {
            return new CloudBackupCredentialReadResult(new CloudBackupCredentials("", ""), true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.KeyBytes);
            CryptographicOperations.ZeroMemory(cipherBytes);
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    private string GetPath()
        => Path.Combine(localStoragePathService.DataDirectory, FileName);

    private static CloudBackupConfigurationDto ToDto(
        StoredCloudBackupConfiguration stored,
        CloudBackupCredentials credentials,
        bool credentialsUnreadable)
        => new(
            stored.Enabled,
            stored.AutoUploadAfterBackup,
            stored.BucketName,
            stored.Endpoint,
            credentialsUnreadable ? "" : Mask(credentials.AccessKey),
            !credentialsUnreadable && !string.IsNullOrWhiteSpace(credentials.AccessKey),
            !credentialsUnreadable && !string.IsNullOrWhiteSpace(credentials.SecretKey),
            stored.Prefix,
            stored.RetentionCount,
            stored.ConnectionTimeoutSeconds,
            stored.RetryCount);

    private static CloudBackupCredentials NormalizeCredentials(
        CloudBackupConfigurationRequest request,
        CloudBackupCredentialReadResult currentCredentials)
        => request.CredentialUpdateMode switch
        {
            CloudBackupCredentialUpdateMode.Clear => new CloudBackupCredentials("", ""),
            CloudBackupCredentialUpdateMode.Replace => new CloudBackupCredentials(
                NormalizeCredentialValue(request.AccessKey, "Access key"),
                NormalizeCredentialValue(request.SecretKey, "Secret key")),
            CloudBackupCredentialUpdateMode.Preserve when currentCredentials.Unreadable => throw new InvalidOperationException(
                "The saved cloud backup credentials could not be decrypted. Replace the credentials and try again."),
            _ => new CloudBackupCredentials(
                string.IsNullOrWhiteSpace(request.AccessKey) ? currentCredentials.Credentials.AccessKey : request.AccessKey.Trim(),
                string.IsNullOrWhiteSpace(request.SecretKey) ? currentCredentials.Credentials.SecretKey : request.SecretKey.Trim())
        };

    private static string NormalizeCredentialValue(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is required when replacing cloud backup credentials.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 512 || HasControlCharacters(trimmed))
        {
            throw new InvalidOperationException($"{label} is invalid.");
        }

        return trimmed;
    }

    private static string NormalizeBucketName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (!BucketNamePattern.IsMatch(trimmed) ||
            trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.Contains(".-", StringComparison.Ordinal) ||
            trimmed.Contains("-.", StringComparison.Ordinal) ||
            IPAddress.TryParse(trimmed, out _))
        {
            throw new InvalidOperationException("Cloud backup bucket name is invalid.");
        }

        return trimmed;
    }

    private static async Task<string> NormalizeEndpointAsync(string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo) ||
            !string.IsNullOrWhiteSpace(uri.Query) ||
            !string.IsNullOrWhiteSpace(uri.Fragment) ||
            !string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            throw new InvalidOperationException("Cloud backup endpoint must be a valid HTTPS origin.");
        }

        if (!R2EndpointHostPattern.IsMatch(uri.Host))
        {
            throw new InvalidOperationException("Cloud backup endpoint must be a Cloudflare R2 account endpoint.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            throw new InvalidOperationException("Cloud backup endpoint could not be resolved.", exception);
        }

        if (addresses.Length == 0 || addresses.Any(IsUnsafeAddress))
        {
            throw new InvalidOperationException("Cloud backup endpoint resolved to an unsafe network address.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string NormalizePrefix(string? value)
    {
        var raw = (value ?? "").Trim();
        if (raw.StartsWith("/", StringComparison.Ordinal) ||
            raw.EndsWith("/", StringComparison.Ordinal) ||
            raw.Contains("..", StringComparison.Ordinal) ||
            HasControlCharacters(raw) ||
            raw.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cloud backup folder prefix is invalid.");
        }

        var prefix = raw.Trim('/');
        return prefix.Length > 200 ? prefix[..200] : prefix;
    }

    private static bool HasControlCharacters(string value)
        => value.Any(char.IsControl);

    private static bool IsUnsafeAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   bytes[0] == 0 ||
                   bytes[0] >= 224;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6Multicast ||
                   address.IsIPv6SiteLocal ||
                   bytes.All(item => item == 0) ||
                   (bytes[0] & 0xfe) == 0xfc;
        }

        return true;
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value.Length <= 4
            ? "****"
            : $"{value[..Math.Min(4, value.Length)]}...{value[^Math.Min(4, value.Length)..]}";
    }

    private static void TryRestrictFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Best-effort filesystem hardening; credentials are still encrypted at rest.
        }
    }

    private sealed class StoredCloudBackupConfiguration
    {
        public bool Enabled { get; set; }

        public bool AutoUploadAfterBackup { get; set; }

        public string BucketName { get; set; } = "";

        public string Endpoint { get; set; } = "";

        public string Prefix { get; set; } = "";

        public int RetentionCount { get; set; } = 30;

        public int ConnectionTimeoutSeconds { get; set; } = 30;

        public int RetryCount { get; set; } = 3;

        public EncryptedCloudBackupCredentials? Credentials { get; set; }
    }

    private sealed record CloudBackupCredentials(string AccessKey, string SecretKey);

    private sealed record CloudBackupCredentialReadResult(
        CloudBackupCredentials Credentials,
        bool Unreadable);

    private sealed record EncryptedCloudBackupCredentials(
        string Algorithm,
        string KeyId,
        string Nonce,
        string Tag,
        string CipherText);
}
