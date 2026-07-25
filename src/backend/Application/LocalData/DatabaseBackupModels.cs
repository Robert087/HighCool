namespace ERP.Application.LocalData;

public enum BackupReason
{
    Manual,
    Scheduled,
    BeforeMigration,
    BeforeRestore,
    BeforeApplicationUpdate
}

public enum BackupStatus
{
    Succeeded,
    Failed,
    Canceled
}

public sealed record BackupResult(
    BackupStatus Status,
    string BackupId,
    DateTime TimestampUtc,
    long SizeBytes,
    string? ChecksumSha256,
    BackupReason Reason,
    string Message,
    string? BackupFileName = null,
    string? ManifestFileName = null);

public interface IDatabaseBackupService
{
    Task<BackupResult> CreateBackupAsync(
        BackupReason reason,
        CancellationToken cancellationToken);
}
