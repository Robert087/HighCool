using ERP.Domain.Common;

namespace ERP.Domain.System;

public sealed class ApplicationDatabaseMetadata : AuditableEntity
{
    public ApplicationDatabaseMetadata()
    {
    }

    public ApplicationDatabaseMetadata(Guid id)
    {
        Id = id;
    }

    public string ApplicationVersion { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public int DatabaseSchemaVersion { get; set; }

    public DateTime DatabaseCreatedAtUtc { get; set; }

    public DateTime LastSuccessfulSchemaUpgradeAtUtc { get; set; }
}
