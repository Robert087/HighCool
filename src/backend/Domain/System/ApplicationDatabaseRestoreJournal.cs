using ERP.Domain.Common;

namespace ERP.Domain.System;

public enum DatabaseRestoreJournalStatus
{
    Started,
    SafetyBackupCreated,
    BackupValidated,
    DatabaseReplaced,
    Verified,
    Completed,
    Failed,
    RolledBack
}

public sealed class ApplicationDatabaseRestoreJournal : AuditableEntity
{
    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string SelectedBackupId { get; set; } = string.Empty;

    public string? SafetyBackupId { get; set; }

    public int? OriginalSchemaVersion { get; set; }

    public int? RestoredSchemaVersion { get; set; }

    public DatabaseRestoreJournalStatus Status { get; set; }

    public string ApplicationVersion { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public string? FailureCode { get; set; }

    public string? FailureMessage { get; set; }
}
