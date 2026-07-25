namespace ERP.Application.LocalData;

public sealed record BackupEncryptionKey(byte[] KeyBytes, string KeyId);

public interface IBackupEncryptionKeyProvider
{
    Task<BackupEncryptionKey> GetOrCreateKeyAsync(CancellationToken cancellationToken);
}

public sealed record BackupManifest(
    int ManifestVersion,
    string BackupId,
    string InstallationId,
    string ApplicationVersion,
    int DatabaseSchemaVersion,
    DateTime CreatedAtUtc,
    BackupReason Reason,
    string DatabaseFileName,
    long DatabaseSizeBytes,
    string PlainSha256,
    string EncryptedSha256,
    BackupEncryptionManifest Encryption);

public sealed record BackupEncryptionManifest(
    string Algorithm,
    string KeyProtection,
    string KeyId,
    string Nonce,
    string Tag);

public enum RestorePreflightStatus
{
    Valid,
    BackupNotFound,
    ManifestInvalid,
    ChecksumMismatch,
    DecryptionFailed,
    CorruptDatabase,
    UnsupportedSchema,
    NewerSchema,
    WrongInstallation,
    InsufficientDiskSpace,
    DestinationUnavailable
}

public sealed record RestoreRequest(string BackupId, string? Confirmation = null);

public sealed record RestorePreflightResult(
    RestorePreflightStatus Status,
    string Message,
    string? BackupId = null,
    int? SchemaVersion = null);

public enum RestoreStatus
{
    Completed,
    Failed,
    Rejected
}

public sealed record RestoreResult(
    RestoreStatus Status,
    string Message,
    string? SelectedBackupId = null,
    string? SafetyBackupId = null);

public interface IDatabaseRestoreService
{
    Task<RestorePreflightResult> ValidateAsync(RestoreRequest request, CancellationToken cancellationToken);

    Task<RestoreResult> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken);
}

public sealed record DatabaseUpgradeRequest(bool Force = false);

public enum DatabaseUpgradeStatus
{
    Completed,
    Noop,
    Failed,
    Rejected
}

public sealed record DatabaseUpgradeResult(
    DatabaseUpgradeStatus Status,
    string Message,
    string? BackupId = null,
    int? FromSchemaVersion = null,
    int? TargetSchemaVersion = null);

public interface IDatabaseUpgradeService
{
    Task<DatabaseUpgradeResult> UpgradeAsync(DatabaseUpgradeRequest request, CancellationToken cancellationToken);
}

public sealed class BackupRetentionOptions
{
    public const string SectionName = "BackupRetention";

    public bool Enabled { get; set; } = true;

    public int ManualCount { get; set; } = 10;

    public int ScheduledCount { get; set; } = 24;

    public int BeforeMigrationCount { get; set; } = 10;

    public int BeforeRestoreCount { get; set; } = 10;

    public int BeforeApplicationUpdateCount { get; set; } = 10;

    public int MinimumAgeHoursBeforeDeletion { get; set; } = 24;
}

public sealed record BackupRetentionResult(
    bool Enabled,
    int DeletedPairs,
    int PreservedFiles,
    IReadOnlyList<string> Messages);

public interface IBackupRetentionService
{
    Task<BackupRetentionResult> ApplyAsync(
        IReadOnlyCollection<string> activeBackupIds,
        CancellationToken cancellationToken);
}

public enum StartupDiagnosticStatus
{
    Starting,
    Healthy,
    DatabaseMissing,
    DatabaseUnavailable,
    DatabaseCorrupt,
    UpgradeRequired,
    UpgradeInProgress,
    UpgradeFailed,
    RestoreRequired,
    ReadOnly,
    UnsupportedSchema,
    ConfigurationInvalid
}

public sealed record StartupDiagnosticResult(
    StartupDiagnosticStatus Status,
    string Title,
    string Message,
    string SupportCode,
    bool RetryAllowed,
    bool BackupAvailable,
    bool RestoreAvailable,
    DateTime TimestampUtc,
    string? DatabaseProvider,
    int? SchemaVersion,
    string? ApplicationVersion,
    DateTime? LastBackupAtUtc,
    string? LastUpgradeStatus,
    string? LastRestoreStatus);

public interface IStartupDiagnosticsService
{
    Task<StartupDiagnosticResult> GetAsync(CancellationToken cancellationToken);
}
