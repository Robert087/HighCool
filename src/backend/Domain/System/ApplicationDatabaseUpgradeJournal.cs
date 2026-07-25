using ERP.Domain.Common;

namespace ERP.Domain.System;

public enum DatabaseUpgradeJournalStatus
{
    Started,
    BackupCreated,
    MigrationsApplied,
    Verified,
    Completed,
    Failed
}

public sealed class ApplicationDatabaseUpgradeJournal : AuditableEntity
{
    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public int FromSchemaVersion { get; set; }

    public int TargetSchemaVersion { get; set; }

    public string? PreUpgradeBackupId { get; set; }

    public DatabaseUpgradeJournalStatus Status { get; set; }

    public string ApplicationVersion { get; set; } = string.Empty;

    public string? FailureCode { get; set; }

    public string? FailureMessage { get; set; }

    public string InstallationId { get; set; } = string.Empty;
}
