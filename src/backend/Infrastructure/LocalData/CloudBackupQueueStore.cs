using ERP.Application.LocalData;
using System.Text.Json;

namespace ERP.Infrastructure.LocalData;

public sealed class CloudBackupQueueStore(ILocalStoragePathService localStoragePathService) : ICloudBackupQueueStore
{
    private const string QueueFileName = "cloud-backup-queue.json";
    private const string QueueBackupFileName = "cloud-backup-queue.json.bak";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<CloudBackupQueueItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken)).Select(ToDto).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CloudBackupQueueItemDto> EnqueueAsync(
        string backupId,
        int maxAttempts,
        bool force,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var existing = items
                .Where(item => string.Equals(item.BackupId, backupId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.QueuedAtUtc)
                .FirstOrDefault(item => item.Status is CloudBackupUploadStatus.Queued or CloudBackupUploadStatus.Uploading or CloudBackupUploadStatus.Uploaded);

            if (existing is not null && !force)
            {
                return ToDto(existing);
            }

            var item = new StoredCloudBackupQueueItem
            {
                QueueId = Guid.NewGuid().ToString("N"),
                BackupId = backupId,
                Status = CloudBackupUploadStatus.Queued,
                FailureCategory = CloudBackupFailureCategory.None,
                Attempts = 0,
                MaxAttempts = Math.Clamp(maxAttempts, 1, 10),
                QueuedAtUtc = DateTime.UtcNow,
                NextAttemptAtUtc = DateTime.UtcNow
            };
            items.Add(item);
            await WriteUnsafeAsync(items, cancellationToken);
            return ToDto(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CloudBackupQueueItemDto?> GetAsync(string queueId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken))
                .Where(item => string.Equals(item.QueueId, queueId, StringComparison.OrdinalIgnoreCase))
                .Select(ToDto)
                .SingleOrDefault();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CloudBackupQueueItemDto?> GetLatestForBackupAsync(string backupId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken))
                .Where(item => string.Equals(item.BackupId, backupId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.QueuedAtUtc)
                .Select(ToDto)
                .FirstOrDefault();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CloudBackupQueueItemDto?> ClaimNextAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var item = items
                .Where(item => item.Status == CloudBackupUploadStatus.Queued &&
                               (item.NextAttemptAtUtc is null || item.NextAttemptAtUtc <= utcNow))
                .OrderBy(item => item.QueuedAtUtc)
                .FirstOrDefault();
            if (item is null)
            {
                return null;
            }

            item.Status = CloudBackupUploadStatus.Uploading;
            item.FailureCategory = CloudBackupFailureCategory.None;
            item.Attempts++;
            item.StartedAtUtc = utcNow;
            item.LastError = null;
            await WriteUnsafeAsync(items, cancellationToken);
            return ToDto(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkSucceededAsync(string queueId, DateTime completedAtUtc, CancellationToken cancellationToken)
    {
        await UpdateAsync(queueId, item =>
        {
            item.Status = CloudBackupUploadStatus.Uploaded;
            item.FailureCategory = CloudBackupFailureCategory.None;
            item.CompletedAtUtc = completedAtUtc;
            item.NextAttemptAtUtc = null;
            item.LastError = null;
        }, cancellationToken);
    }

    public async Task MarkFailedAsync(
        string queueId,
        CloudBackupFailureCategory failureCategory,
        string safeMessage,
        DateTime utcNow,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        await UpdateAsync(queueId, item =>
        {
            item.FailureCategory = failureCategory;
            item.LastError = safeMessage;
            if (item.Attempts >= item.MaxAttempts || retryDelay <= TimeSpan.Zero)
            {
                item.Status = CloudBackupUploadStatus.Failed;
                item.CompletedAtUtc = utcNow;
                item.NextAttemptAtUtc = null;
            }
            else
            {
                item.Status = CloudBackupUploadStatus.Queued;
                item.NextAttemptAtUtc = utcNow.Add(retryDelay);
            }
        }, cancellationToken);
    }

    public Task MarkCanceledAsync(string queueId, CancellationToken cancellationToken)
        => UpdateAsync(queueId, item =>
        {
            item.Status = CloudBackupUploadStatus.Canceled;
            item.FailureCategory = CloudBackupFailureCategory.Cancellation;
            item.CompletedAtUtc = DateTime.UtcNow;
            item.NextAttemptAtUtc = null;
        }, cancellationToken);

    private async Task UpdateAsync(
        string queueId,
        Action<StoredCloudBackupQueueItem> update,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var item = items.SingleOrDefault(item => string.Equals(item.QueueId, queueId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Cloud upload queue item was not found.");
            update(item);
            await WriteUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<StoredCloudBackupQueueItem>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        localStoragePathService.EnsureRequiredDirectories();
        var path = GetPath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return await ReadFileAsync(path, cancellationToken);
        }
        catch (JsonException primaryException)
        {
            var backupPath = GetBackupPath();
            if (File.Exists(backupPath))
            {
                try
                {
                    return await ReadFileAsync(backupPath, cancellationToken);
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException("Cloud upload queue is corrupted and recovery failed.", primaryException);
                }
            }

            throw new InvalidOperationException("Cloud upload queue is corrupted.", primaryException);
        }
    }

    private async Task WriteUnsafeAsync(
        IReadOnlyList<StoredCloudBackupQueueItem> items,
        CancellationToken cancellationToken)
    {
        localStoragePathService.EnsureRequiredDirectories();
        var path = GetPath();
        var backupPath = GetBackupPath();
        var tempPath = Path.Combine(localStoragePathService.DataDirectory, $"{QueueFileName}.{Guid.NewGuid():N}.tmp");
        var payload = JsonSerializer.Serialize(items, JsonOptions);

        await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(payload.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            File.Copy(path, backupPath, overwrite: true);
            TryRestrictFilePermissions(backupPath);
        }

        File.Move(tempPath, path, overwrite: true);
        TryRestrictFilePermissions(path);
    }

    private string GetPath()
        => Path.Combine(localStoragePathService.DataDirectory, QueueFileName);

    private string GetBackupPath()
        => Path.Combine(localStoragePathService.DataDirectory, QueueBackupFileName);

    private static async Task<List<StoredCloudBackupQueueItem>> ReadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<StoredCloudBackupQueueItem>>(
            stream,
            JsonOptions,
            cancellationToken) ?? [];
    }

    private static CloudBackupQueueItemDto ToDto(StoredCloudBackupQueueItem item)
        => new(
            item.QueueId,
            item.BackupId,
            item.Status,
            item.FailureCategory,
            item.Attempts,
            item.MaxAttempts,
            item.QueuedAtUtc,
            item.StartedAtUtc,
            item.CompletedAtUtc,
            item.NextAttemptAtUtc,
            item.LastError);

    private sealed class StoredCloudBackupQueueItem
    {
        public string QueueId { get; set; } = "";

        public string BackupId { get; set; } = "";

        public CloudBackupUploadStatus Status { get; set; }

        public CloudBackupFailureCategory FailureCategory { get; set; }

        public int Attempts { get; set; }

        public int MaxAttempts { get; set; } = 3;

        public DateTime QueuedAtUtc { get; set; }

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public DateTime? NextAttemptAtUtc { get; set; }

        public string? LastError { get; set; }
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
        }
    }
}
