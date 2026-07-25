namespace ERP.Application.LocalData;

public sealed record DatabaseMetadataDto(
    string ApplicationVersion,
    string InstallationId,
    int DatabaseSchemaVersion,
    DateTime DatabaseCreatedAtUtc,
    DateTime LastSuccessfulSchemaUpgradeAtUtc);

public interface IApplicationDatabaseMetadataService
{
    Task<DatabaseMetadataDto> EnsureInitializedAsync(CancellationToken cancellationToken);

    Task<DatabaseMetadataDto?> GetCurrentAsync(CancellationToken cancellationToken);
}
