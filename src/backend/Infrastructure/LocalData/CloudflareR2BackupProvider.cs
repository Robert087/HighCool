using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using ERP.Application.Common.Pagination;
using ERP.Application.LocalData;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.RegularExpressions;

namespace ERP.Infrastructure.LocalData;

public sealed partial class CloudflareR2BackupProvider(
    ILogger<CloudflareR2BackupProvider> logger) : ICloudBackupProvider
{
    private const string PayloadKind = "payload";
    private const string ManifestKind = "manifest";
    private const string ConnectionTestContent = "highcool-r2-connection-test";

    public async Task<CloudBackupProviderConnectionTestResult> TestConnectionAsync(
        CloudBackupSettings settings,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings, CloudBackupConnectionTestStage.Validation);
        var testKey = BuildKey(settings, $".connection-test/{Guid.NewGuid():N}.tmp");
        var context = CloudBackupLogContext.From(settings);
        logger.LogInformation(
            "Cloud backup connection test started. Operation={Operation} EndpointHost={EndpointHost} Bucket={Bucket} Prefix={Prefix}",
            "connection-test",
            context.EndpointHost,
            context.BucketName,
            context.Prefix);

        IAmazonS3 client;
        try
        {
            client = CreateClient(settings);
        }
        catch (Exception exception) when (IsSafeProviderException(exception))
        {
            throw CreateConnectionException(exception, CloudBackupConnectionTestStage.ClientCreation);
        }

        using (client)
        {
            return await TestConnectionWithClientAsync(client, settings, testKey, cancellationToken);
        }
    }

    private async Task<CloudBackupProviderConnectionTestResult> TestConnectionWithClientAsync(
        IAmazonS3 client,
        CloudBackupSettings settings,
        string testKey,
        CancellationToken cancellationToken)
    {
        var uploaded = false;
        CloudBackupConnectionException? operationFailure = null;
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = settings.BucketName,
                Key = testKey,
                ContentBody = ConnectionTestContent,
                ContentType = "text/plain",
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            }, cancellationToken);
            uploaded = true;

            await using var response = await client.GetObjectStreamAsync(settings.BucketName, testKey, null, cancellationToken);
            using var reader = new StreamReader(response);
            var content = await reader.ReadToEndAsync(cancellationToken);
            if (!string.Equals(content, ConnectionTestContent, StringComparison.Ordinal))
            {
                throw new CloudBackupConnectionException(
                    CloudBackupConnectionFailureCategory.ContentVerificationFailed,
                    CloudBackupConnectionTestStage.ChecksumVerification,
                    "The test object was read back, but the content did not match.");
            }
        }
        catch (CloudBackupConnectionException exception)
        {
            operationFailure = exception;
        }
        catch (Exception exception) when (IsSafeProviderException(exception))
        {
            operationFailure = CreateConnectionException(
                exception,
                uploaded ? CloudBackupConnectionTestStage.Read : CloudBackupConnectionTestStage.Write);
        }
        finally
        {
            if (uploaded)
            {
                try
                {
                    await client.DeleteObjectAsync(settings.BucketName, testKey, CancellationToken.None);
                }
                catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
                {
                }
                catch (Exception exception) when (IsSafeProviderException(exception))
                {
                    var cleanupFailure = CreateConnectionException(
                        exception,
                        CloudBackupConnectionTestStage.DeleteCleanup,
                        CloudBackupConnectionFailureCategory.CleanupFailed,
                        "The test object was written and read, but could not be deleted.");
                    operationFailure = operationFailure is null
                        ? cleanupFailure
                        : new CloudBackupConnectionException(
                            operationFailure.Category,
                            operationFailure.Stage,
                            operationFailure.Message,
                            operationFailure.StatusCode,
                            operationFailure.ProviderErrorCode,
                            cleanupSucceeded: false,
                            operationFailure.InnerException);
                }
            }
        }

        var logContext = CloudBackupLogContext.From(settings);
        if (operationFailure is not null)
        {
            logger.LogWarning(
                "Cloud backup connection test failed. Operation={Operation} EndpointHost={EndpointHost} Bucket={Bucket} Prefix={Prefix} Category={Category} Stage={Stage} StatusCode={StatusCode} ProviderErrorCode={ProviderErrorCode} CleanupSucceeded={CleanupSucceeded}",
                "connection-test",
                logContext.EndpointHost,
                logContext.BucketName,
                logContext.Prefix,
                operationFailure.Category,
                operationFailure.Stage,
                operationFailure.StatusCode,
                operationFailure.ProviderErrorCode,
                operationFailure.CleanupSucceeded);
            throw operationFailure;
        }

        logger.LogInformation(
            "Cloud backup connection test succeeded. Operation={Operation} EndpointHost={EndpointHost} Bucket={Bucket} Prefix={Prefix} CleanupSucceeded={CleanupSucceeded}",
            "connection-test",
            logContext.EndpointHost,
            logContext.BucketName,
            logContext.Prefix,
            true);
        return new CloudBackupProviderConnectionTestResult();
    }

    public async Task UploadAsync(
        CloudBackupSettings settings,
        BackupManifest manifest,
        string payloadPath,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings);
        if (!File.Exists(payloadPath) || !File.Exists(manifestPath))
        {
            throw new InvalidOperationException("Local backup files are missing.");
        }

        using var client = CreateClient(settings);
        var payloadKey = BuildPayloadKey(settings, manifest.BackupId, manifest.DatabaseFileName);
        var manifestKey = BuildManifestKey(settings, manifest.BackupId, Path.GetFileName(manifestPath));
        var payloadMetadata = BuildMetadata(manifest, PayloadKind, payloadKey, manifestKey);
        var manifestMetadata = BuildMetadata(manifest, ManifestKind, payloadKey, manifestKey);

        await UploadFileAsync(client, settings.BucketName, payloadKey, payloadPath, payloadMetadata, cancellationToken);
        await UploadFileAsync(client, settings.BucketName, manifestKey, manifestPath, manifestMetadata, cancellationToken);
    }

    public async Task<IReadOnlyList<CloudBackupObject>> ListAsync(
        CloudBackupSettings settings,
        CloudBackupListQuery query,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings);
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var prefix = BuildKey(settings, "backups/");
        var manifestSuffix = BackupManifestService.ManifestExtension;
        var results = new List<CloudBackupObject>();
        string? continuationToken = null;
        var seen = 0;
        var target = pagination.Skip + pagination.NormalizedPageSize;

        using var client = CreateClient(settings);
        do
        {
            var response = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = settings.BucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken,
                MaxKeys = Math.Min(1000, target)
            }, cancellationToken);

            foreach (var item in response.S3Objects.Where(item => item.Key.EndsWith(manifestSuffix, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(query.Search) &&
                    !item.Key.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                seen++;
                if (seen <= pagination.Skip)
                {
                    continue;
                }

                var metadata = await client.GetObjectMetadataAsync(settings.BucketName, item.Key, cancellationToken);
                results.Add(ToCloudObject(item, metadata));
                if (results.Count >= pagination.NormalizedPageSize)
                {
                    return Sort(results, query.SortBy, query.SortDirection);
                }
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null && results.Count < pagination.NormalizedPageSize);

        return Sort(results, query.SortBy, query.SortDirection);
    }

    public async Task<bool> ExistsAsync(
        CloudBackupSettings settings,
        string backupId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings);
        using var client = CreateClient(settings);
        try
        {
            _ = await FindManifestKeyAsync(client, settings, backupId, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or AmazonS3Exception)
        {
            return false;
        }
    }

    public async Task<bool> ExistsObjectAsync(
        CloudBackupSettings settings,
        string objectKey,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings);
        using var client = CreateClient(settings);
        try
        {
            _ = await client.GetObjectMetadataAsync(settings.BucketName, EnsureManagedObjectKey(settings, objectKey), cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DownloadManifestAsync(
        CloudBackupSettings settings,
        string backupId,
        string manifestDestinationPath,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings);
        using var client = CreateClient(settings);
        var manifestKey = await FindManifestKeyAsync(client, settings, backupId, cancellationToken);
        await DownloadFileAsync(client, settings.BucketName, manifestKey, manifestDestinationPath, cancellationToken);
    }

    public async Task DownloadObjectAsync(
        CloudBackupSettings settings,
        string objectKey,
        string payloadDestinationPath,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings);
        using var client = CreateClient(settings);
        await DownloadFileAsync(client, settings.BucketName, EnsureManagedObjectKey(settings, objectKey), payloadDestinationPath, cancellationToken);
    }

    public async Task DeleteAsync(
        CloudBackupSettings settings,
        string backupId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured(settings);
        using var client = CreateClient(settings);
        var manifestKey = await FindManifestKeyAsync(client, settings, backupId, cancellationToken);
        var metadata = await client.GetObjectMetadataAsync(settings.BucketName, manifestKey, cancellationToken);
        var payloadKey = GetMetadataValue(metadata, "payload-object-key");

        if (!string.IsNullOrWhiteSpace(payloadKey))
        {
            await client.DeleteObjectAsync(settings.BucketName, EnsureManagedObjectKey(settings, payloadKey), cancellationToken);
        }

        await client.DeleteObjectAsync(settings.BucketName, manifestKey, cancellationToken);
    }

    private static async Task UploadFileAsync(
        IAmazonS3 client,
        string bucketName,
        string key,
        string path,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var request = new TransferUtilityUploadRequest
        {
            BucketName = bucketName,
            Key = key,
            FilePath = path,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        foreach (var (name, value) in metadata)
        {
            request.Metadata.Add(name, value);
        }

        using var transfer = new TransferUtility(client);
        await transfer.UploadAsync(request, cancellationToken);
    }

    private static async Task DownloadFileAsync(
        IAmazonS3 client,
        string bucketName,
        string key,
        string path,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var transfer = new TransferUtility(client);
        await transfer.DownloadAsync(path, bucketName, key, cancellationToken);
    }

    private static IAmazonS3 CreateClient(CloudBackupSettings settings)
    {
        var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
        return new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            Timeout = TimeSpan.FromSeconds(settings.ConnectionTimeoutSeconds),
            MaxErrorRetry = Math.Max(0, settings.RetryCount)
        });
    }

    private static void EnsureConfigured(
        CloudBackupSettings settings,
        CloudBackupConnectionTestStage stage = CloudBackupConnectionTestStage.Validation)
    {
        if (!settings.Enabled)
        {
            throw new CloudBackupConnectionException(
                CloudBackupConnectionFailureCategory.InvalidConfiguration,
                stage,
                "Cloud backup is disabled.");
        }

        if (!settings.IsConfigured)
        {
            var category = string.IsNullOrWhiteSpace(settings.AccessKey) || string.IsNullOrWhiteSpace(settings.SecretKey)
                ? CloudBackupConnectionFailureCategory.CredentialsMissing
                : CloudBackupConnectionFailureCategory.InvalidConfiguration;
            throw new CloudBackupConnectionException(
                category,
                category == CloudBackupConnectionFailureCategory.CredentialsMissing
                    ? CloudBackupConnectionTestStage.Credentials
                    : stage,
                category == CloudBackupConnectionFailureCategory.CredentialsMissing
                    ? "Cloud backup credentials are missing."
                    : "Cloud backup is not configured.");
        }

        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new CloudBackupConnectionException(
                CloudBackupConnectionFailureCategory.InvalidConfiguration,
                stage,
                "Cloud backup endpoint must use HTTPS.");
        }
    }

    private static CloudBackupConnectionException CreateConnectionException(
        Exception exception,
        CloudBackupConnectionTestStage stage,
        CloudBackupConnectionFailureCategory? forcedCategory = null,
        string? forcedMessage = null)
    {
        var amazon = FindException<AmazonServiceException>(exception);
        var category = forcedCategory ?? CategorizeProviderFailure(exception, amazon, stage);
        return new CloudBackupConnectionException(
            category,
            stage,
            forcedMessage ?? ToSafeProviderMessage(category),
            amazon?.StatusCode is null ? null : (int)amazon.StatusCode,
            SanitizeProviderErrorCode(amazon?.ErrorCode),
            cleanupSucceeded: stage != CloudBackupConnectionTestStage.DeleteCleanup,
            exception);
    }

    private static CloudBackupConnectionFailureCategory CategorizeProviderFailure(
        Exception exception,
        AmazonServiceException? amazon,
        CloudBackupConnectionTestStage stage)
    {
        if (FindException<TimeoutException>(exception) is not null ||
            exception is TaskCanceledException or OperationCanceledException)
        {
            return CloudBackupConnectionFailureCategory.Timeout;
        }

        if (FindException<AuthenticationException>(exception) is not null)
        {
            return CloudBackupConnectionFailureCategory.TlsFailure;
        }

        if (FindException<SocketException>(exception) is { } socket)
        {
            return socket.SocketErrorCode == SocketError.HostNotFound ||
                   socket.SocketErrorCode == SocketError.NoData ||
                   socket.SocketErrorCode == SocketError.TryAgain
                ? CloudBackupConnectionFailureCategory.DnsFailure
                : CloudBackupConnectionFailureCategory.NetworkUnavailable;
        }

        if (amazon is not null)
        {
            if (IsCredentialError(amazon))
            {
                return CloudBackupConnectionFailureCategory.InvalidCredentials;
            }

            if (amazon.StatusCode == HttpStatusCode.NotFound ||
                string.Equals(amazon.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
            {
                return CloudBackupConnectionFailureCategory.BucketNotFound;
            }

            if (amazon.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                return stage switch
                {
                    CloudBackupConnectionTestStage.Write => CloudBackupConnectionFailureCategory.WriteDenied,
                    CloudBackupConnectionTestStage.Read => CloudBackupConnectionFailureCategory.ReadDenied,
                    CloudBackupConnectionTestStage.DeleteCleanup => CloudBackupConnectionFailureCategory.DeleteDenied,
                    _ => CloudBackupConnectionFailureCategory.AccessDenied
                };
            }

            if ((int)amazon.StatusCode == 400)
            {
                return CloudBackupConnectionFailureCategory.EndpointRejected;
            }

            if ((int)amazon.StatusCode is 408 or 429 or >= 500)
            {
                return CloudBackupConnectionFailureCategory.NetworkUnavailable;
            }
        }

        return stage switch
        {
            CloudBackupConnectionTestStage.Write => CloudBackupConnectionFailureCategory.WriteDenied,
            CloudBackupConnectionTestStage.Read => CloudBackupConnectionFailureCategory.ReadDenied,
            CloudBackupConnectionTestStage.DeleteCleanup => CloudBackupConnectionFailureCategory.DeleteDenied,
            _ => CloudBackupConnectionFailureCategory.UnknownProviderFailure
        };
    }

    private static bool IsCredentialError(AmazonServiceException exception)
        => exception.StatusCode == HttpStatusCode.Unauthorized ||
           ErrorCodeEquals(exception, "InvalidAccessKeyId") ||
           ErrorCodeEquals(exception, "SignatureDoesNotMatch") ||
           ErrorCodeEquals(exception, "AuthFailure") ||
           ErrorCodeEquals(exception, "InvalidToken") ||
           ErrorCodeEquals(exception, "ExpiredToken");

    private static bool ErrorCodeEquals(AmazonServiceException exception, string value)
        => string.Equals(exception.ErrorCode, value, StringComparison.OrdinalIgnoreCase);

    private static string ToSafeProviderMessage(CloudBackupConnectionFailureCategory category)
        => category switch
        {
            CloudBackupConnectionFailureCategory.InvalidCredentials => "The R2 credentials are invalid.",
            CloudBackupConnectionFailureCategory.AccessDenied => "The R2 token does not have access to this bucket.",
            CloudBackupConnectionFailureCategory.BucketNotFound => "The bucket was not found in this R2 account.",
            CloudBackupConnectionFailureCategory.EndpointRejected => "Cloudflare R2 rejected the endpoint or request.",
            CloudBackupConnectionFailureCategory.DnsFailure => "Cloudflare R2 could not be resolved.",
            CloudBackupConnectionFailureCategory.TlsFailure => "Cloudflare R2 could not be reached over TLS.",
            CloudBackupConnectionFailureCategory.Timeout => "The Cloudflare R2 connection timed out.",
            CloudBackupConnectionFailureCategory.NetworkUnavailable => "Cloudflare R2 could not be reached.",
            CloudBackupConnectionFailureCategory.WriteDenied => "The token does not have write access to this bucket.",
            CloudBackupConnectionFailureCategory.ReadDenied => "The token does not have read access to this bucket.",
            CloudBackupConnectionFailureCategory.DeleteDenied => "The token does not have delete access to this bucket.",
            CloudBackupConnectionFailureCategory.ContentVerificationFailed => "The test object content did not match after reading it back.",
            CloudBackupConnectionFailureCategory.CleanupFailed => "The test object was written and read, but could not be deleted.",
            _ => "Cloud backup is currently unavailable."
        };

    private static bool IsSafeProviderException(Exception exception)
        => exception is AmazonServiceException or InvalidOperationException or IOException or HttpRequestException or TimeoutException or OperationCanceledException ||
           FindException<SocketException>(exception) is not null ||
           FindException<AuthenticationException>(exception) is not null;

    private static TException? FindException<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TException match)
            {
                return match;
            }

            if (current is AggregateException aggregate)
            {
                var nested = aggregate.InnerExceptions.Select(FindException<TException>).FirstOrDefault(item => item is not null);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? SanitizeProviderErrorCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return ProviderErrorCodePattern().IsMatch(trimmed) ? trimmed[..Math.Min(trimmed.Length, 80)] : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderErrorCodePattern();

    private sealed record CloudBackupLogContext(string EndpointHost, string BucketName, string Prefix)
    {
        public static CloudBackupLogContext From(CloudBackupSettings settings)
            => new(
                Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : "",
                settings.BucketName,
                string.IsNullOrWhiteSpace(settings.Prefix) ? "(root)" : settings.Prefix);
    }

    private static Dictionary<string, string> BuildMetadata(
        BackupManifest manifest,
        string objectKind,
        string payloadKey,
        string manifestKey)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["backup-id"] = manifest.BackupId,
            ["created-at-utc"] = manifest.CreatedAtUtc.ToString("O"),
            ["checksum-sha256"] = manifest.EncryptedSha256,
            ["database-size-bytes"] = manifest.DatabaseSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["object-kind"] = objectKind,
            ["payload-object-key"] = payloadKey,
            ["manifest-object-key"] = manifestKey,
            ["application-version"] = manifest.ApplicationVersion,
            ["schema-version"] = manifest.DatabaseSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    private static CloudBackupObject ToCloudObject(S3Object item, GetObjectMetadataResponse metadata)
    {
        var backupId = GetMetadataValue(metadata, "backup-id") ?? ExtractBackupId(item.Key);
        var createdAt = DateTime.TryParse(GetMetadataValue(metadata, "created-at-utc"), out var parsed)
            ? parsed.ToUniversalTime()
            : item.LastModified.ToUniversalTime();
        var size = long.TryParse(GetMetadataValue(metadata, "database-size-bytes"), out var parsedSize)
            ? parsedSize
            : item.Size;

        return new CloudBackupObject(
            backupId,
            item.Key,
            createdAt,
            size,
            GetMetadataValue(metadata, "checksum-sha256"),
            GetMetadataValue(metadata, "payload-object-key"),
            item.Key);
    }

    private static IReadOnlyList<CloudBackupObject> Sort(
        List<CloudBackupObject> items,
        string? sortBy,
        SortDirection direction)
    {
        var ordered = (sortBy ?? "").Trim().ToLowerInvariant() switch
        {
            "backupid" => items.OrderBy(item => item.BackupId),
            "sizebytes" => items.OrderBy(item => item.SizeBytes),
            _ => items.OrderBy(item => item.CreatedAtUtc)
        };

        return (direction == SortDirection.Desc ? ordered.Reverse() : ordered).ToList();
    }

    private static async Task<string> FindManifestKeyAsync(
        IAmazonS3 client,
        CloudBackupSettings settings,
        string backupId,
        CancellationToken cancellationToken)
    {
        var expectedPrefix = BuildKey(settings, $"backups/{SanitizeBackupId(backupId)}/");
        var response = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = settings.BucketName,
            Prefix = expectedPrefix,
            MaxKeys = 10
        }, cancellationToken);
        var manifestKey = response.S3Objects
            .Select(item => item.Key)
            .SingleOrDefault(key => key.EndsWith(BackupManifestService.ManifestExtension, StringComparison.OrdinalIgnoreCase));

        return manifestKey ?? throw new InvalidOperationException("Cloud backup was not found.");
    }

    private static string BuildPayloadKey(CloudBackupSettings settings, string backupId, string fileName)
        => BuildKey(settings, $"backups/{SanitizeBackupId(backupId)}/{Path.GetFileName(fileName)}");

    public static string BuildExpectedPayloadKey(string prefix, string backupId, string fileName)
        => BuildKey(prefix, $"backups/{SanitizeBackupId(backupId)}/{Path.GetFileName(fileName)}");

    public static string BuildExpectedManifestKey(string prefix, string backupId, string fileName)
        => BuildKey(prefix, $"backups/{SanitizeBackupId(backupId)}/{Path.GetFileName(fileName)}");

    private static string BuildManifestKey(CloudBackupSettings settings, string backupId, string? fileName)
        => fileName is null
            ? BuildKey(settings, $"backups/{SanitizeBackupId(backupId)}/")
            : BuildKey(settings, $"backups/{SanitizeBackupId(backupId)}/{Path.GetFileName(fileName)}");

    private static string BuildKey(CloudBackupSettings settings, string suffix)
        => BuildKey(settings.Prefix, suffix);

    private static string BuildKey(string prefixValue, string suffix)
    {
        var prefix = prefixValue.Trim('/');
        return string.IsNullOrWhiteSpace(prefix)
            ? suffix.TrimStart('/')
            : $"{prefix}/{suffix.TrimStart('/')}";
    }

    private static string EnsureManagedObjectKey(CloudBackupSettings settings, string objectKey)
    {
        var normalized = objectKey.Trim('/');
        var expectedPrefix = BuildKey(settings, "backups/");
        if (!normalized.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cloud backup object key is outside the managed prefix.");
        }

        return normalized;
    }

    private static string SanitizeBackupId(string backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId) || backupId.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException("Backup ID is invalid.");
        }

        return backupId;
    }

    private static string ExtractBackupId(string objectKey)
    {
        var parts = objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(parts, part => string.Equals(part, "backups", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : "";
    }

    private static string? GetMetadataValue(GetObjectMetadataResponse metadata, string key)
    {
        foreach (var name in metadata.Metadata.Keys)
        {
            var normalized = name.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase)
                ? name["x-amz-meta-".Length..]
                : name;
            if (string.Equals(normalized, key, StringComparison.OrdinalIgnoreCase))
            {
                return metadata.Metadata[name];
            }
        }

        return null;
    }
}
