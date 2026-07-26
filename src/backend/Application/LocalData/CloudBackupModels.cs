using ERP.Application.Common.Pagination;

namespace ERP.Application.LocalData;

public enum CloudBackupStatus
{
    Disabled,
    NotConfigured,
    Ready,
    Offline,
    Error
}

public enum CloudBackupUploadStatus
{
    NotQueued,
    Queued,
    Uploading,
    Uploaded,
    Failed,
    Canceled
}

public enum CloudBackupObjectStatus
{
    Missing,
    Present
}

public enum CloudBackupSyncStatus
{
    LocalOnly,
    CloudOnly,
    InSync,
    OutOfSync,
    Queued,
    Uploading,
    Failed,
    Downloading,
    LegacyUntrusted,
    Corrupted,
    MissingRemotePayload,
    MissingRemoteManifest
}

public enum CloudBackupFailureCategory
{
    None,
    TransientNetworkFailure,
    Timeout,
    Throttling,
    ServiceUnavailable,
    DnsFailure,
    AuthenticationFailure,
    AuthorizationFailure,
    InvalidBucket,
    InvalidEndpoint,
    MissingLocalFile,
    ChecksumMismatch,
    InvalidManifest,
    Cancellation,
    QueueCorruption
}

public enum CloudBackupConnectionFailureCategory
{
    None,
    InvalidConfiguration,
    CredentialsMissing,
    CredentialsUnreadable,
    InvalidCredentials,
    AccessDenied,
    BucketNotFound,
    EndpointRejected,
    DnsFailure,
    TlsFailure,
    Timeout,
    NetworkUnavailable,
    WriteDenied,
    ReadDenied,
    DeleteDenied,
    ContentVerificationFailed,
    CleanupFailed,
    UnknownProviderFailure
}

public enum CloudBackupConnectionTestStage
{
    Validation,
    Credentials,
    ClientCreation,
    List,
    Write,
    Read,
    ChecksumVerification,
    DeleteCleanup,
    Completed
}

public enum CloudBackupCredentialUpdateMode
{
    Preserve,
    Replace,
    Clear
}

public sealed record CloudBackupConfigurationDto(
    bool Enabled,
    bool AutoUploadAfterBackup,
    string BucketName,
    string Endpoint,
    string AccessKey,
    bool HasAccessKey,
    bool HasSecretKey,
    string Prefix,
    int RetentionCount,
    int ConnectionTimeoutSeconds,
    int RetryCount);

public sealed record CloudBackupConfigurationRequest(
    bool Enabled,
    bool AutoUploadAfterBackup,
    string BucketName,
    string Endpoint,
    string? AccessKey,
    string? SecretKey,
    string? Prefix,
    int RetentionCount,
    int ConnectionTimeoutSeconds,
    int RetryCount,
    CloudBackupCredentialUpdateMode CredentialUpdateMode = CloudBackupCredentialUpdateMode.Preserve);

public sealed record CloudBackupSettings(
    bool Enabled,
    bool AutoUploadAfterBackup,
    string BucketName,
    string Endpoint,
    string AccessKey,
    string SecretKey,
    string Prefix,
    int RetentionCount,
    int ConnectionTimeoutSeconds,
    int RetryCount)
{
    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(BucketName) &&
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(AccessKey) &&
        !string.IsNullOrWhiteSpace(SecretKey);
}

public sealed record CloudBackupStatusDto(
    CloudBackupStatus Status,
    string Message,
    bool Enabled,
    bool Configured,
    int QueuedCount,
    int UploadingCount,
    int FailedCount,
    DateTime? LastSuccessfulUploadAtUtc);

public sealed class CloudBackupConnectionTestResultDto
{
    public CloudBackupConnectionTestResultDto(
        bool success,
        CloudBackupConnectionFailureCategory category,
        string message,
        CloudBackupConnectionTestStage stage,
        int? statusCode,
        string? providerErrorCode,
        bool cleanupSucceeded,
        DateTime testedAtUtc)
    {
        Success = success;
        Category = category;
        Message = message;
        Stage = stage;
        StatusCode = statusCode;
        ProviderErrorCode = providerErrorCode;
        CleanupSucceeded = cleanupSucceeded;
        TestedAtUtc = testedAtUtc;
    }

    public bool Success { get; }

    public bool Succeeded => Success;

    public string Status => Category == CloudBackupConnectionFailureCategory.None ? "Succeeded" : Category.ToString();

    public CloudBackupConnectionFailureCategory Category { get; }

    public string Message { get; }

    public CloudBackupConnectionTestStage Stage { get; }

    public int? StatusCode { get; }

    public string? ProviderErrorCode { get; }

    public bool CleanupSucceeded { get; }

    public DateTime TestedAtUtc { get; }
}

public sealed record CloudBackupProviderConnectionTestResult(
    bool CleanupSucceeded = true);

public sealed record CloudBackupQueueItemDto(
    string QueueId,
    string BackupId,
    CloudBackupUploadStatus Status,
    CloudBackupFailureCategory FailureCategory,
    int Attempts,
    int MaxAttempts,
    DateTime QueuedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? NextAttemptAtUtc,
    string? LastError);

public sealed record CloudBackupListItemDto(
    string BackupId,
    DateTime CreatedAtUtc,
    long SizeBytes,
    string? ChecksumSha256,
    BackupStatus LocalStatus,
    CloudBackupObjectStatus CloudStatus,
    CloudBackupUploadStatus UploadStatus,
    BackupIntegrityStatus VerificationStatus,
    CloudBackupSyncStatus SyncStatus,
    DateTime? LastUploadedAtUtc,
    string? CloudObjectKey);

public sealed record CloudBackupDownloadResultDto(
    string BackupId,
    bool Downloaded,
    string Message,
    BackupIntegrityStatus IntegrityStatus);

public sealed record CloudBackupUploadRequest(bool Force = false);

public sealed record CloudBackupListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);

public sealed record CloudBackupObject(
    string BackupId,
    string ObjectKey,
    DateTime CreatedAtUtc,
    long SizeBytes,
    string? ChecksumSha256,
    string? PayloadObjectKey,
    string? ManifestObjectKey,
    bool IsAuthenticated = false,
    bool PayloadExists = true,
    int? ManifestVersion = null);

public interface ICloudBackupConfigurationStore
{
    Task<CloudBackupConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken);

    Task<CloudBackupConfigurationDto> SaveConfigurationAsync(
        CloudBackupConfigurationRequest request,
        CancellationToken cancellationToken);

    Task<CloudBackupSettings> GetSettingsAsync(CancellationToken cancellationToken);
}

public interface ICloudBackupProvider
{
    Task<CloudBackupProviderConnectionTestResult> TestConnectionAsync(
        CloudBackupSettings settings,
        CancellationToken cancellationToken);

    Task UploadAsync(
        CloudBackupSettings settings,
        BackupManifest manifest,
        string payloadPath,
        string manifestPath,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CloudBackupObject>> ListAsync(
        CloudBackupSettings settings,
        CloudBackupListQuery query,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        CloudBackupSettings settings,
        string backupId,
        CancellationToken cancellationToken);

    Task<bool> ExistsObjectAsync(
        CloudBackupSettings settings,
        string objectKey,
        CancellationToken cancellationToken);

    Task DownloadManifestAsync(
        CloudBackupSettings settings,
        string backupId,
        string manifestDestinationPath,
        CancellationToken cancellationToken);

    Task DownloadObjectAsync(
        CloudBackupSettings settings,
        string objectKey,
        string payloadDestinationPath,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        CloudBackupSettings settings,
        string backupId,
        CancellationToken cancellationToken);
}

public interface ICloudBackupQueueStore
{
    Task<IReadOnlyList<CloudBackupQueueItemDto>> ListAsync(CancellationToken cancellationToken);

    Task<CloudBackupQueueItemDto> EnqueueAsync(
        string backupId,
        int maxAttempts,
        bool force,
        CancellationToken cancellationToken);

    Task<CloudBackupQueueItemDto?> GetAsync(string queueId, CancellationToken cancellationToken);

    Task<CloudBackupQueueItemDto?> GetLatestForBackupAsync(string backupId, CancellationToken cancellationToken);

    Task<CloudBackupQueueItemDto?> ClaimNextAsync(DateTime utcNow, CancellationToken cancellationToken);

    Task MarkSucceededAsync(string queueId, DateTime completedAtUtc, CancellationToken cancellationToken);

    Task MarkFailedAsync(
        string queueId,
        CloudBackupFailureCategory failureCategory,
        string safeMessage,
        DateTime utcNow,
        TimeSpan retryDelay,
        CancellationToken cancellationToken);

    Task MarkCanceledAsync(string queueId, CancellationToken cancellationToken);
}

public interface ICloudBackupWorkflowService
{
    Task<CloudBackupStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    Task<CloudBackupConnectionTestResultDto> TestConnectionAsync(CancellationToken cancellationToken);

    Task<CloudBackupQueueItemDto> EnqueueUploadAsync(
        string backupId,
        bool force,
        CancellationToken cancellationToken);

    Task<CloudBackupQueueItemDto> RetryUploadAsync(string queueId, CancellationToken cancellationToken);

    Task<CloudBackupQueueItemDto> CancelUploadAsync(string queueId, CancellationToken cancellationToken);

    Task ProcessDueUploadsAsync(CancellationToken cancellationToken);

    Task<PagedResult<CloudBackupListItemDto>> ListCloudBackupsAsync(
        CloudBackupListQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<CloudBackupListItemDto>> ListCombinedBackupsAsync(
        CloudBackupListQuery query,
        CancellationToken cancellationToken);

    Task<CloudBackupDownloadResultDto> DownloadAsync(
        string backupId,
        CancellationToken cancellationToken);

    Task DeleteCloudBackupAsync(string backupId, CancellationToken cancellationToken);
}
