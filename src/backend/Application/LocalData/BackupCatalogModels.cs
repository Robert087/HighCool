namespace ERP.Application.LocalData;

public enum BackupHealthStatus
{
    Healthy,
    Warning,
    Error,
    Unknown
}

public enum BackupIntegrityStatus
{
    Unknown,
    Verified,
    Failed
}

public sealed record BackupHealthReasonDto(string Code, string Message);

public sealed record BackupRetentionSettingsDto(
    bool Enabled,
    int ManualCount,
    int ScheduledCount,
    int BeforeMigrationCount,
    int BeforeRestoreCount,
    int BeforeApplicationUpdateCount,
    int MinimumAgeHoursBeforeDeletion);

public sealed record BackupCenterSummaryDto(
    BackupHealthStatus Health,
    IReadOnlyList<BackupHealthReasonDto> HealthReasons,
    DateTime? LastSuccessfulBackupAtUtc,
    DateTime? LastIntegrityVerificationAtUtc,
    string? DatabaseFileName,
    long? DatabaseSizeBytes,
    int AvailableBackupCount,
    long BackupStorageUsedBytes,
    string EncryptionStatus,
    bool RetentionEnabled,
    string RetentionStatus,
    string? ApplicationVersion,
    int? DatabaseSchemaVersion,
    BackupRetentionSettingsDto RetentionSettings);

public sealed record BackupListItemDto(
    string BackupId,
    DateTime CreatedAtUtc,
    BackupReason Reason,
    BackupStatus Status,
    long SizeBytes,
    string? ApplicationVersion,
    int? DatabaseSchemaVersion,
    BackupIntegrityStatus IntegrityStatus,
    DateTime? LastVerifiedAtUtc);

public sealed record BackupDetailsDto(
    string BackupId,
    DateTime CreatedAtUtc,
    BackupReason Reason,
    BackupStatus Status,
    string ApplicationVersion,
    int DatabaseSchemaVersion,
    int ManifestVersion,
    string EncryptionAlgorithm,
    long BackupSizeBytes,
    long OriginalDatabaseSizeBytes,
    string CompressionStatus,
    string EncryptedSha256,
    string PlainSha256,
    BackupIntegrityStatus IntegrityStatus,
    DateTime? LastVerifiedAtUtc,
    RestorePreflightStatus? RestoreCompatibilityStatus,
    string? RestoreCompatibilityMessage,
    string DatabaseFileName);

public sealed record BackupIntegrityVerificationResultDto(
    string BackupId,
    BackupIntegrityStatus Status,
    DateTime VerifiedAtUtc,
    string Message);

public interface IBackupCatalogService
{
    Task<BackupCenterSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BackupListItemDto>> ListAsync(CancellationToken cancellationToken);

    Task<BackupDetailsDto> GetDetailsAsync(string backupId, CancellationToken cancellationToken);

    Task<BackupIntegrityVerificationResultDto> VerifyAsync(string backupId, CancellationToken cancellationToken);

    Task<BackupRetentionSettingsDto> GetRetentionSettingsAsync(CancellationToken cancellationToken);

    Task<BackupRetentionSettingsDto> SaveRetentionSettingsAsync(
        BackupRetentionSettingsDto settings,
        CancellationToken cancellationToken);
}
