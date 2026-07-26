using ERP.Application.LocalData;

namespace ERP.Application.TestData;

public enum OrganizationTestDataCommandStatus
{
    Planned,
    Completed,
    Rejected,
    Failed
}

public sealed record OrganizationTestDataCommandResult(
    OrganizationTestDataCommandStatus Status,
    string Message,
    Guid OrganizationId,
    string Profile,
    string RunId,
    bool DryRun,
    string? ManifestPath,
    string? SnapshotPath,
    string? SafetyBackupId,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<string> Warnings);

public sealed record SeedOrganizationTestDataRequest(
    Guid OrganizationId,
    string Profile,
    string Scale,
    int Seed,
    bool DryRun,
    bool Force);

public sealed record ResetOrganizationDataRequest(
    Guid OrganizationId,
    bool DryRun,
    bool Execute,
    string? Confirmation,
    bool PreserveUsers,
    bool PreserveOrganization,
    bool PreserveSettings,
    bool TestDataOnly,
    string? SeedRunId,
    bool SkipSafetyBackup);

public sealed record VerifyOrganizationRestoreRequest(
    Guid OrganizationId,
    string SnapshotPath);

public sealed record OrganizationDataSnapshot(
    int Version,
    Guid OrganizationId,
    string Profile,
    string RunId,
    DateTime CreatedAtUtc,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyDictionary<string, decimal> Totals);

public sealed record OrganizationTestDataManifest(
    int Version,
    Guid OrganizationId,
    string Profile,
    string Scale,
    int Seed,
    string RunId,
    string Marker,
    DateTime CreatedAtUtc,
    IReadOnlyDictionary<string, IReadOnlyList<Guid>> EntityIds,
    OrganizationDataSnapshot Snapshot);

public interface IOrganizationTestDataService
{
    Task<OrganizationTestDataCommandResult> SeedAsync(
        SeedOrganizationTestDataRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationTestDataCommandResult> ResetAsync(
        ResetOrganizationDataRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationTestDataCommandResult> VerifyAsync(
        VerifyOrganizationRestoreRequest request,
        CancellationToken cancellationToken);
}

public interface IOrganizationScopedToolExecutionContext
{
    void SetOrganization(Guid organizationId);
}
