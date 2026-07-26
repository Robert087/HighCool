using ERP.Application.Common.Pagination;
using ERP.Application.LocalData;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.LocalData;

public sealed class CloudBackupWorkflowService(
    ICloudBackupConfigurationStore configurationStore,
    ICloudBackupProvider provider,
    ICloudBackupQueueStore queueStore,
    IBackupCatalogService backupCatalogService,
    ILocalStoragePathService localStoragePathService,
    BackupManifestService manifestService,
    IBackupManifestAuthenticationService manifestAuthenticationService,
    ILogger<CloudBackupWorkflowService> logger) : ICloudBackupWorkflowService
{
    private readonly SemaphoreSlim _uploaderGate = new(1, 1);

    public async Task<CloudBackupStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var settings = await configurationStore.GetSettingsAsync(cancellationToken);
        var queue = await queueStore.ListAsync(cancellationToken);
        var lastSuccess = queue
            .Where(item => item.Status == CloudBackupUploadStatus.Uploaded)
            .MaxBy(item => item.CompletedAtUtc ?? item.QueuedAtUtc)
            ?.CompletedAtUtc;
        var configured = settings.IsConfigured;
        var status = !settings.Enabled
            ? CloudBackupStatus.Disabled
            : configured
                ? CloudBackupStatus.Ready
                : CloudBackupStatus.NotConfigured;
        var message = status switch
        {
            CloudBackupStatus.Disabled => "Cloud backup is disabled.",
            CloudBackupStatus.NotConfigured => "Cloud backup credentials are not configured.",
            _ => "Cloud backup is ready."
        };

        return new CloudBackupStatusDto(
            status,
            message,
            settings.Enabled,
            configured,
            queue.Count(item => item.Status == CloudBackupUploadStatus.Queued),
            queue.Count(item => item.Status == CloudBackupUploadStatus.Uploading),
            queue.Count(item => item.Status == CloudBackupUploadStatus.Failed),
            lastSuccess);
    }

    public async Task<CloudBackupConnectionTestResultDto> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var testedAt = DateTime.UtcNow;
        CloudBackupSettings? settings = null;
        try
        {
            settings = await configurationStore.GetSettingsAsync(cancellationToken);
            LogConnectionTestStarted(settings);
            ValidateConnectionTestSettings(settings);
            var result = await provider.TestConnectionAsync(settings, cancellationToken);
            LogConnectionTestSucceeded(settings, result.CleanupSucceeded);
            return new CloudBackupConnectionTestResultDto(
                true,
                CloudBackupConnectionFailureCategory.None,
                "Cloud backup connection verified.",
                CloudBackupConnectionTestStage.Completed,
                null,
                null,
                result.CleanupSucceeded,
                testedAt);
        }
        catch (CloudBackupConnectionException exception)
        {
            LogConnectionTestFailed(settings, exception);
            return new CloudBackupConnectionTestResultDto(
                false,
                exception.Category,
                exception.Message,
                exception.Stage,
                exception.StatusCode,
                exception.ProviderErrorCode,
                exception.CleanupSucceeded,
                testedAt);
        }
        catch (Exception exception) when (IsSafeCloudException(exception))
        {
            var category = CategorizeConnectionTestException(exception);
            var failure = new CloudBackupConnectionException(
                category,
                CloudBackupConnectionTestStage.Validation,
                ToFriendlyConnectionTestMessage(category),
                cleanupSucceeded: false,
                innerException: exception);
            LogConnectionTestFailed(settings, failure);
            return new CloudBackupConnectionTestResultDto(
                false,
                failure.Category,
                failure.Message,
                failure.Stage,
                failure.StatusCode,
                failure.ProviderErrorCode,
                failure.CleanupSucceeded,
                testedAt);
        }
    }

    public async Task<CloudBackupQueueItemDto> EnqueueUploadAsync(
        string backupId,
        bool force,
        CancellationToken cancellationToken)
    {
        _ = await LoadLocalBackupAsync(backupId, cancellationToken);
        var settings = await configurationStore.GetSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Cloud backup is disabled.");
        }

        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("Cloud backup is not configured.");
        }

        return await queueStore.EnqueueAsync(backupId, settings.RetryCount, force, cancellationToken);
    }

    public async Task<CloudBackupQueueItemDto> RetryUploadAsync(string queueId, CancellationToken cancellationToken)
    {
        var item = await queueStore.GetAsync(queueId, cancellationToken)
            ?? throw new InvalidOperationException("Cloud upload queue item was not found.");
        return await queueStore.EnqueueAsync(item.BackupId, item.MaxAttempts, true, cancellationToken);
    }

    public async Task<CloudBackupQueueItemDto> CancelUploadAsync(string queueId, CancellationToken cancellationToken)
    {
        await queueStore.MarkCanceledAsync(queueId, cancellationToken);
        return await queueStore.GetAsync(queueId, cancellationToken)
            ?? throw new InvalidOperationException("Cloud upload queue item was not found.");
    }

    public async Task ProcessDueUploadsAsync(CancellationToken cancellationToken)
    {
        if (!await _uploaderGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var queueItem = await queueStore.ClaimNextAsync(DateTime.UtcNow, cancellationToken);
                if (queueItem is null)
                {
                    return;
                }

                try
                {
                    var settings = await configurationStore.GetSettingsAsync(cancellationToken);
                    if (!settings.Enabled || !settings.IsConfigured)
                    {
                        await queueStore.MarkFailedAsync(
                            queueItem.QueueId,
                            CloudBackupFailureCategory.InvalidEndpoint,
                            "Cloud backup is disabled or not configured.",
                            DateTime.UtcNow,
                            TimeSpan.Zero,
                            cancellationToken);
                        continue;
                    }

                    var local = await EnsureLocalManifestSignedAsync(
                        await LoadLocalBackupAsync(queueItem.BackupId, cancellationToken),
                        settings,
                        cancellationToken);
                    var localChecksum = await SqliteDatabaseBackupService.CalculateChecksumAsync(local.PayloadPath, cancellationToken);
                    if (!string.Equals(localChecksum, local.Manifest.EncryptedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Local backup checksum mismatch.");
                    }

                    await provider.UploadAsync(settings, local.Manifest, local.PayloadPath, local.ManifestPath, cancellationToken);
                    await queueStore.MarkSucceededAsync(queueItem.QueueId, DateTime.UtcNow, cancellationToken);
                    await ApplyCloudRetentionAsync(settings, cancellationToken);
                }
                catch (Exception exception) when (IsSafeCloudException(exception))
                {
                    var category = Categorize(exception);
                    await queueStore.MarkFailedAsync(
                        queueItem.QueueId,
                        category,
                        ToFriendlyMessage(exception),
                        DateTime.UtcNow,
                        IsRetryable(category) ? CalculateBackoff(queueItem.Attempts) : TimeSpan.Zero,
                        CancellationToken.None);
                }
            }
        }
        finally
        {
            _uploaderGate.Release();
        }
    }

    public async Task<PagedResult<CloudBackupListItemDto>> ListCloudBackupsAsync(
        CloudBackupListQuery query,
        CancellationToken cancellationToken)
    {
        var settings = await configurationStore.GetSettingsAsync(cancellationToken);
        if (!settings.Enabled || !settings.IsConfigured)
        {
            return EmptyPage(query, new { query.Search });
        }

        var cloudObjects = await ListVerifiedCloudBackupsAsync(settings, query.Search, query.SortBy, query.SortDirection, cancellationToken);
        var local = await backupCatalogService.ListAsync(cancellationToken);
        var localById = local.ToDictionary(item => item.BackupId, StringComparer.OrdinalIgnoreCase);
        var localManifests = await LoadLocalManifestMapAsync(cancellationToken);
        var queue = await queueStore.ListAsync(cancellationToken);
        var rows = cloudObjects.Select(item =>
        {
            localById.TryGetValue(item.BackupId, out var localBackup);
            var upload = LatestUploadStatus(queue, item.BackupId);
            return new CloudBackupListItemDto(
                item.BackupId,
                item.CreatedAtUtc,
                item.SizeBytes,
                item.ChecksumSha256,
                localBackup?.Status ?? BackupStatus.Failed,
                CloudBackupObjectStatus.Present,
                upload,
                localBackup?.IntegrityStatus ?? BackupIntegrityStatus.Unknown,
                localBackup is null ? GetCloudOnlySyncStatus(item) : GetSyncStatus(localManifests.GetValueOrDefault(item.BackupId), item, upload),
                queue.FirstOrDefault(queueItem => queueItem.Status == CloudBackupUploadStatus.Uploaded && queueItem.BackupId == item.BackupId)?.CompletedAtUtc,
                item.ObjectKey);
        }).ToList();

        return ToPage(rows, query, new { query.Search });
    }

    public async Task<PagedResult<CloudBackupListItemDto>> ListCombinedBackupsAsync(
        CloudBackupListQuery query,
        CancellationToken cancellationToken)
    {
        var local = await backupCatalogService.ListAsync(cancellationToken);
        var settings = await configurationStore.GetSettingsAsync(cancellationToken);
        var cloudObjects = settings.Enabled && settings.IsConfigured
            ? await ListVerifiedCloudBackupsAsync(settings, query.Search, query.SortBy, query.SortDirection, cancellationToken)
            : [];
        var cloudById = cloudObjects.ToDictionary(item => item.BackupId, StringComparer.OrdinalIgnoreCase);
        var localManifests = await LoadLocalManifestMapAsync(cancellationToken);
        var queue = await queueStore.ListAsync(cancellationToken);
        var rows = local.Select(item =>
        {
            cloudById.TryGetValue(item.BackupId, out var cloud);
            var upload = LatestUploadStatus(queue, item.BackupId);
            return new CloudBackupListItemDto(
                item.BackupId,
                item.CreatedAtUtc,
                item.SizeBytes,
                cloud?.ChecksumSha256,
                item.Status,
                cloud is null ? CloudBackupObjectStatus.Missing : CloudBackupObjectStatus.Present,
                upload,
                item.IntegrityStatus,
                cloud is null ? SyncStatusFromUpload(upload, CloudBackupSyncStatus.LocalOnly) : GetSyncStatus(localManifests.GetValueOrDefault(item.BackupId), cloud, upload),
                queue.FirstOrDefault(queueItem => queueItem.Status == CloudBackupUploadStatus.Uploaded && queueItem.BackupId == item.BackupId)?.CompletedAtUtc,
                cloud?.ObjectKey);
        }).ToList();

        foreach (var cloud in cloudObjects.Where(item => local.All(localItem => !string.Equals(localItem.BackupId, item.BackupId, StringComparison.OrdinalIgnoreCase))))
        {
            rows.Add(new CloudBackupListItemDto(
                cloud.BackupId,
                cloud.CreatedAtUtc,
                cloud.SizeBytes,
                cloud.ChecksumSha256,
                BackupStatus.Failed,
                CloudBackupObjectStatus.Present,
                LatestUploadStatus(queue, cloud.BackupId),
                BackupIntegrityStatus.Unknown,
                GetCloudOnlySyncStatus(cloud),
                null,
                cloud.ObjectKey));
        }

        return ToPage(Sort(rows, query.SortBy, query.SortDirection), query, new { query.Search });
    }

    public async Task<CloudBackupDownloadResultDto> DownloadAsync(
        string backupId,
        CancellationToken cancellationToken)
    {
        var settings = await configurationStore.GetSettingsAsync(cancellationToken);
        if (!settings.Enabled || !settings.IsConfigured)
        {
            throw new InvalidOperationException("Cloud backup is not configured.");
        }

        localStoragePathService.EnsureRequiredDirectories();
        var operationId = Guid.NewGuid().ToString("N");
        var tempManifestPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{backupId}.{operationId}.cloud.manifest.tmp");
        var tempPayloadPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{backupId}.{operationId}.cloud.payload.tmp");

        try
        {
            await provider.DownloadManifestAsync(settings, backupId, tempManifestPath, cancellationToken);
            var manifest = await manifestService.ReadAndValidateAsync(tempManifestPath, cancellationToken);
            if (!string.Equals(manifest.BackupId, backupId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cloud backup manifest does not match the requested backup.");
            }

            var relativePayloadKey = CloudflareR2BackupProvider.BuildExpectedPayloadKey("", manifest.BackupId, manifest.DatabaseFileName);
            var relativeManifestKey = CloudflareR2BackupProvider.BuildExpectedManifestKey("", manifest.BackupId, GetManifestFileName(manifest.DatabaseFileName));
            await manifestAuthenticationService.ValidateAsync(manifest, relativePayloadKey, relativeManifestKey, cancellationToken);
            var payloadKey = CloudflareR2BackupProvider.BuildExpectedPayloadKey(settings.Prefix, manifest.BackupId, manifest.DatabaseFileName);
            await provider.DownloadObjectAsync(settings, payloadKey, tempPayloadPath, cancellationToken);

            var checksum = await SqliteDatabaseBackupService.CalculateChecksumAsync(tempPayloadPath, cancellationToken);
            if (!string.Equals(checksum, manifest.EncryptedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cloud backup checksum mismatch.");
            }

            if (manifest.EncryptedSizeBytes.HasValue && new FileInfo(tempPayloadPath).Length != manifest.EncryptedSizeBytes.Value)
            {
                throw new InvalidOperationException("Cloud backup size mismatch.");
            }

            var finalPayloadPath = Path.Combine(localStoragePathService.BackupDirectory, manifest.DatabaseFileName);
            var finalManifestPath = manifestService.GetManifestPathForBackupFile(finalPayloadPath);
            await EnsureDownloadedBackupCanBePlacedAsync(finalPayloadPath, checksum, cancellationToken);
            File.Move(tempPayloadPath, finalPayloadPath, overwrite: false);
            File.Move(tempManifestPath, finalManifestPath, overwrite: true);

            var verification = await backupCatalogService.VerifyAsync(backupId, cancellationToken);
            return new CloudBackupDownloadResultDto(
                backupId,
                true,
                verification.Status == BackupIntegrityStatus.Verified
                    ? "Cloud backup downloaded and verified."
                    : "Cloud backup downloaded but integrity verification failed.",
                verification.Status);
        }
        finally
        {
            DeleteIfExists(tempManifestPath);
            DeleteIfExists(tempPayloadPath);
        }
    }

    public async Task DeleteCloudBackupAsync(string backupId, CancellationToken cancellationToken)
    {
        var settings = await configurationStore.GetSettingsAsync(cancellationToken);
        await provider.DeleteAsync(settings, backupId, cancellationToken);
    }

    private async Task ApplyCloudRetentionAsync(CloudBackupSettings settings, CancellationToken cancellationToken)
    {
        if (settings.RetentionCount <= 0)
        {
            return;
        }

        var queue = await queueStore.ListAsync(cancellationToken);
        var activeBackupIds = queue
            .Where(item => item.Status is CloudBackupUploadStatus.Queued or CloudBackupUploadStatus.Uploading)
            .Select(item => item.BackupId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var backups = await ListVerifiedCloudBackupsAsync(settings, null, "createdAtUtc", SortDirection.Desc, cancellationToken);

        foreach (var backup in backups
                     .Where(item => item.IsAuthenticated && item.PayloadExists && !activeBackupIds.Contains(item.BackupId))
                     .OrderByDescending(item => item.CreatedAtUtc)
                     .Skip(settings.RetentionCount))
        {
            await provider.DeleteAsync(settings, backup.BackupId, cancellationToken);
        }
    }

    private async Task<LocalBackupFiles> EnsureLocalManifestSignedAsync(
        LocalBackupFiles local,
        CloudBackupSettings settings,
        CancellationToken cancellationToken)
    {
        if (local.Manifest.Authentication is not null)
        {
            return local;
        }

        var signed = await manifestAuthenticationService.SignAsync(
            local.Manifest,
            CloudflareR2BackupProvider.BuildExpectedPayloadKey("", local.Manifest.BackupId, local.Manifest.DatabaseFileName),
            CloudflareR2BackupProvider.BuildExpectedManifestKey("", local.Manifest.BackupId, Path.GetFileName(local.ManifestPath)),
            cancellationToken);
        await manifestService.WriteAsync(local.ManifestPath, signed, cancellationToken);
        return local with { Manifest = signed };
    }

    private async Task<IReadOnlyList<CloudBackupObject>> ListVerifiedCloudBackupsAsync(
        CloudBackupSettings settings,
        string? search,
        string? sortBy,
        SortDirection sortDirection,
        CancellationToken cancellationToken)
    {
        var results = new List<CloudBackupObject>();
        for (var page = 1; ; page++)
        {
            var candidates = await provider.ListAsync(
                settings,
                new CloudBackupListQuery(page, 100, search, sortBy, sortDirection),
                cancellationToken);
            foreach (var candidate in candidates)
            {
                results.Add(await VerifyCloudObjectAsync(settings, candidate, cancellationToken));
            }

            if (candidates.Count < 100)
            {
                break;
            }
        }

        return results;
    }

    private async Task<CloudBackupObject> VerifyCloudObjectAsync(
        CloudBackupSettings settings,
        CloudBackupObject candidate,
        CancellationToken cancellationToken)
    {
        var tempManifestPath = Path.Combine(localStoragePathService.PendingBackupDirectory, $"{candidate.BackupId}.{Guid.NewGuid():N}.cloud-list.manifest.tmp");
        try
        {
            await provider.DownloadManifestAsync(settings, candidate.BackupId, tempManifestPath, cancellationToken);
            var manifest = await manifestService.ReadAndValidateAsync(tempManifestPath, cancellationToken);
            var relativePayloadKey = CloudflareR2BackupProvider.BuildExpectedPayloadKey("", manifest.BackupId, manifest.DatabaseFileName);
            var relativeManifestKey = CloudflareR2BackupProvider.BuildExpectedManifestKey("", manifest.BackupId, Path.GetFileName(candidate.ObjectKey));
            await manifestAuthenticationService.ValidateAsync(manifest, relativePayloadKey, relativeManifestKey, cancellationToken);
            var payloadKey = CloudflareR2BackupProvider.BuildExpectedPayloadKey(settings.Prefix, manifest.BackupId, manifest.DatabaseFileName);
            var payloadExists = await provider.ExistsObjectAsync(settings, payloadKey, cancellationToken);
            return candidate with
            {
                BackupId = manifest.BackupId,
                CreatedAtUtc = manifest.CreatedAtUtc,
                SizeBytes = manifest.EncryptedSizeBytes ?? candidate.SizeBytes,
                ChecksumSha256 = manifest.EncryptedSha256,
                PayloadObjectKey = payloadKey,
                ManifestObjectKey = candidate.ObjectKey,
                IsAuthenticated = true,
                PayloadExists = payloadExists,
                ManifestVersion = manifest.ManifestVersion
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return candidate with { IsAuthenticated = false, PayloadExists = false };
        }
        finally
        {
            DeleteIfExists(tempManifestPath);
        }
    }

    private async Task<Dictionary<string, BackupManifest>> LoadLocalManifestMapAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, BackupManifest>(StringComparer.OrdinalIgnoreCase);
        localStoragePathService.EnsureRequiredDirectories();
        foreach (var manifestPath in manifestService.EnumerateManifestPaths(localStoragePathService.BackupDirectory))
        {
            try
            {
                var manifest = await manifestService.ReadAndValidateAsync(manifestPath, cancellationToken);
                result[manifest.BackupId] = manifest;
            }
            catch (InvalidOperationException)
            {
            }
        }

        return result;
    }

    private async Task<LocalBackupFiles> LoadLocalBackupAsync(string backupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backupId) || backupId.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException("Backup ID is invalid.");
        }

        localStoragePathService.EnsureRequiredDirectories();
        foreach (var manifestPath in manifestService.EnumerateManifestPaths(localStoragePathService.BackupDirectory))
        {
            var manifest = await manifestService.ReadAndValidateAsync(manifestPath, cancellationToken);
            if (!string.Equals(manifest.BackupId, backupId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payloadPath = Path.Combine(localStoragePathService.BackupDirectory, manifest.DatabaseFileName);
            if (!File.Exists(payloadPath))
            {
                throw new InvalidOperationException("Local backup payload was not found.");
            }

            return new LocalBackupFiles(manifest, manifestPath, payloadPath);
        }

        throw new InvalidOperationException("Local backup was not found.");
    }

    private static PagedResult<CloudBackupListItemDto> EmptyPage(CloudBackupListQuery query, object filters)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        return new PagedResult<CloudBackupListItemDto>(
            [],
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            0,
            0,
            filters,
            new PagedSort(query.SortBy ?? "createdAtUtc", query.SortDirection));
    }

    private static PagedResult<CloudBackupListItemDto> ToPage(
        IReadOnlyList<CloudBackupListItemDto> rows,
        CloudBackupListQuery query,
        object filters)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var sorted = Sort(rows, query.SortBy, query.SortDirection);
        var pageRows = sorted.Skip(pagination.Skip).Take(pagination.NormalizedPageSize).ToList();
        var totalPages = rows.Count == 0
            ? 0
            : (int)Math.Ceiling(rows.Count / (double)pagination.NormalizedPageSize);
        return new PagedResult<CloudBackupListItemDto>(
            pageRows,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            rows.Count,
            totalPages,
            filters,
            new PagedSort(query.SortBy ?? "createdAtUtc", query.SortDirection));
    }

    private static IReadOnlyList<CloudBackupListItemDto> Sort(
        IReadOnlyList<CloudBackupListItemDto> rows,
        string? sortBy,
        SortDirection direction)
    {
        var ordered = (sortBy ?? "").Trim().ToLowerInvariant() switch
        {
            "backupid" => rows.OrderBy(item => item.BackupId),
            "sizebytes" => rows.OrderBy(item => item.SizeBytes),
            "syncstatus" => rows.OrderBy(item => item.SyncStatus),
            _ => rows.OrderBy(item => item.CreatedAtUtc)
        };

        return (direction == SortDirection.Desc ? ordered.Reverse() : ordered).ToList();
    }

    private static CloudBackupUploadStatus LatestUploadStatus(
        IReadOnlyList<CloudBackupQueueItemDto> queue,
        string backupId)
        => queue
            .Where(item => string.Equals(item.BackupId, backupId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.QueuedAtUtc)
            .FirstOrDefault()
            ?.Status ?? CloudBackupUploadStatus.NotQueued;

    private static CloudBackupSyncStatus GetSyncStatus(
        BackupManifest? local,
        CloudBackupObject cloud,
        CloudBackupUploadStatus uploadStatus)
    {
        var uploadSync = SyncStatusFromUpload(uploadStatus, CloudBackupSyncStatus.InSync);
        if (uploadSync != CloudBackupSyncStatus.InSync)
        {
            return uploadSync;
        }

        if (!cloud.IsAuthenticated)
        {
            return CloudBackupSyncStatus.LegacyUntrusted;
        }

        if (!cloud.PayloadExists)
        {
            return CloudBackupSyncStatus.MissingRemotePayload;
        }

        if (local is null)
        {
            return CloudBackupSyncStatus.OutOfSync;
        }

        return string.Equals(local.BackupId, cloud.BackupId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(local.EncryptedSha256, cloud.ChecksumSha256, StringComparison.OrdinalIgnoreCase) &&
               local.EncryptedSizeBytes == cloud.SizeBytes &&
               local.ManifestVersion == cloud.ManifestVersion
            ? CloudBackupSyncStatus.InSync
            : CloudBackupSyncStatus.OutOfSync;
    }

    private static CloudBackupSyncStatus GetCloudOnlySyncStatus(CloudBackupObject cloud)
    {
        if (!cloud.IsAuthenticated)
        {
            return CloudBackupSyncStatus.LegacyUntrusted;
        }

        return cloud.PayloadExists ? CloudBackupSyncStatus.CloudOnly : CloudBackupSyncStatus.MissingRemotePayload;
    }

    private static CloudBackupSyncStatus SyncStatusFromUpload(
        CloudBackupUploadStatus uploadStatus,
        CloudBackupSyncStatus fallback)
        => uploadStatus switch
        {
            CloudBackupUploadStatus.Queued => CloudBackupSyncStatus.Queued,
            CloudBackupUploadStatus.Uploading => CloudBackupSyncStatus.Uploading,
            CloudBackupUploadStatus.Failed => CloudBackupSyncStatus.Failed,
            _ => fallback
        };

    private static TimeSpan CalculateBackoff(int attempts)
        => TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Max(1, attempts)) * 5));

    private static bool IsSafeCloudException(Exception exception)
        => exception is CloudBackupConnectionException or IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or Amazon.S3.AmazonS3Exception or Amazon.Runtime.AmazonServiceException or OperationCanceledException;

    private static void ValidateConnectionTestSettings(CloudBackupSettings settings)
    {
        if (!settings.Enabled)
        {
            throw new CloudBackupConnectionException(
                CloudBackupConnectionFailureCategory.InvalidConfiguration,
                CloudBackupConnectionTestStage.Validation,
                "Cloud backup is disabled.",
                cleanupSucceeded: false);
        }

        if (string.IsNullOrWhiteSpace(settings.AccessKey) || string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            throw new CloudBackupConnectionException(
                CloudBackupConnectionFailureCategory.CredentialsMissing,
                CloudBackupConnectionTestStage.Credentials,
                "Cloud backup credentials are missing.",
                cleanupSucceeded: false);
        }

        if (!settings.IsConfigured)
        {
            throw new CloudBackupConnectionException(
                CloudBackupConnectionFailureCategory.InvalidConfiguration,
                CloudBackupConnectionTestStage.Validation,
                "Cloud backup settings are incomplete or invalid.",
                cleanupSucceeded: false);
        }
    }

    private static CloudBackupConnectionFailureCategory CategorizeConnectionTestException(Exception exception)
    {
        var message = exception.Message;
        if (exception is OperationCanceledException || exception is TimeoutException || message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupConnectionFailureCategory.Timeout;
        }

        if (message.Contains("could not be decrypted", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupConnectionFailureCategory.CredentialsUnreadable;
        }

        if (message.Contains("credential", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupConnectionFailureCategory.CredentialsMissing;
        }

        if (message.Contains("endpoint", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("configured", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("disabled", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupConnectionFailureCategory.InvalidConfiguration;
        }

        return CloudBackupConnectionFailureCategory.UnknownProviderFailure;
    }

    private static CloudBackupFailureCategory Categorize(Exception exception)
    {
        var message = exception.Message;
        if (exception is OperationCanceledException)
        {
            return CloudBackupFailureCategory.Cancellation;
        }

        if (message.Contains("checksum", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.ChecksumMismatch;
        }

        if (message.Contains("manifest", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.InvalidManifest;
        }

        if (message.Contains("local backup", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.MissingLocalFile;
        }

        if (message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("signature", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.AuthenticationFailure;
        }

        if (message.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.AuthorizationFailure;
        }

        if (message.Contains("bucket", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.InvalidBucket;
        }

        if (message.Contains("endpoint", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("resolve", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.InvalidEndpoint;
        }

        if (exception is TimeoutException || message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.Timeout;
        }

        if (message.Contains("throttl", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.Throttling;
        }

        if (message.Contains("service unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.ServiceUnavailable;
        }

        if (message.Contains("dns", StringComparison.OrdinalIgnoreCase))
        {
            return CloudBackupFailureCategory.DnsFailure;
        }

        return CloudBackupFailureCategory.TransientNetworkFailure;
    }

    private static string ToFriendlyMessage(Exception exception)
        => Categorize(exception) switch
        {
            CloudBackupFailureCategory.AuthenticationFailure => "Cloud backup credentials were rejected.",
            CloudBackupFailureCategory.AuthorizationFailure => "Cloud backup permission was denied.",
            CloudBackupFailureCategory.InvalidBucket => "Cloud backup bucket was not found or is unavailable.",
            CloudBackupFailureCategory.InvalidEndpoint => "Cloud backup endpoint is invalid or unavailable.",
            CloudBackupFailureCategory.Timeout => "Cloud backup connection timed out.",
            CloudBackupFailureCategory.ChecksumMismatch => "Cloud backup checksum mismatch.",
            CloudBackupFailureCategory.InvalidManifest => "Cloud backup manifest is invalid or untrusted.",
            CloudBackupFailureCategory.MissingLocalFile => "Local backup files are missing.",
            CloudBackupFailureCategory.Cancellation => "Cloud backup operation was canceled.",
            _ => "Cloud backup is currently unavailable."
        };

    private static string ToFriendlyConnectionTestMessage(CloudBackupConnectionFailureCategory category)
        => category switch
        {
            CloudBackupConnectionFailureCategory.InvalidConfiguration => "Cloud backup settings are incomplete or invalid.",
            CloudBackupConnectionFailureCategory.CredentialsMissing => "Cloud backup credentials are missing.",
            CloudBackupConnectionFailureCategory.CredentialsUnreadable => "The saved credentials could not be decrypted. Replace the credentials and try again.",
            CloudBackupConnectionFailureCategory.Timeout => "The Cloudflare R2 connection timed out.",
            _ => "Cloud backup is currently unavailable."
        };

    private void LogConnectionTestStarted(CloudBackupSettings settings)
    {
        var context = CloudBackupConnectionLogContext.From(settings);
        logger.LogInformation(
            "Cloud backup connection test requested. Operation={Operation} EndpointHost={EndpointHost} Bucket={Bucket} Prefix={Prefix}",
            "connection-test",
            context.EndpointHost,
            context.BucketName,
            context.Prefix);
    }

    private void LogConnectionTestSucceeded(CloudBackupSettings settings, bool cleanupSucceeded)
    {
        var context = CloudBackupConnectionLogContext.From(settings);
        logger.LogInformation(
            "Cloud backup connection test completed. Operation={Operation} EndpointHost={EndpointHost} Bucket={Bucket} Prefix={Prefix} Category={Category} Stage={Stage} CleanupSucceeded={CleanupSucceeded}",
            "connection-test",
            context.EndpointHost,
            context.BucketName,
            context.Prefix,
            CloudBackupConnectionFailureCategory.None,
            CloudBackupConnectionTestStage.Completed,
            cleanupSucceeded);
    }

    private void LogConnectionTestFailed(CloudBackupSettings? settings, CloudBackupConnectionException exception)
    {
        var context = CloudBackupConnectionLogContext.From(settings);
        logger.LogWarning(
            "Cloud backup connection test failed. Operation={Operation} EndpointHost={EndpointHost} Bucket={Bucket} Prefix={Prefix} Category={Category} Stage={Stage} StatusCode={StatusCode} ProviderErrorCode={ProviderErrorCode} CleanupSucceeded={CleanupSucceeded}",
            "connection-test",
            context.EndpointHost,
            context.BucketName,
            context.Prefix,
            exception.Category,
            exception.Stage,
            exception.StatusCode,
            exception.ProviderErrorCode,
            exception.CleanupSucceeded);
    }

    private static bool IsRetryable(CloudBackupFailureCategory category)
        => category is CloudBackupFailureCategory.TransientNetworkFailure
            or CloudBackupFailureCategory.Timeout
            or CloudBackupFailureCategory.Throttling
            or CloudBackupFailureCategory.ServiceUnavailable
            or CloudBackupFailureCategory.DnsFailure;

    private static string GetManifestFileName(string databaseFileName)
        => $"{Path.GetFileNameWithoutExtension(databaseFileName)}{BackupManifestService.ManifestExtension}";

    private static async Task EnsureDownloadedBackupCanBePlacedAsync(
        string finalPayloadPath,
        string downloadedChecksum,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(finalPayloadPath))
        {
            return;
        }

        var existingChecksum = await SqliteDatabaseBackupService.CalculateChecksumAsync(finalPayloadPath, cancellationToken);
        if (!string.Equals(existingChecksum, downloadedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A different local backup already exists for this cloud backup.");
        }

        File.Delete(finalPayloadPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record LocalBackupFiles(
        BackupManifest Manifest,
        string ManifestPath,
        string PayloadPath);

    private sealed record CloudBackupConnectionLogContext(string EndpointHost, string BucketName, string Prefix)
    {
        public static CloudBackupConnectionLogContext From(CloudBackupSettings? settings)
            => settings is null
                ? new CloudBackupConnectionLogContext("", "", "")
                : new CloudBackupConnectionLogContext(
                    Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : "",
                    settings.BucketName,
                    string.IsNullOrWhiteSpace(settings.Prefix) ? "(root)" : settings.Prefix);
    }
}
