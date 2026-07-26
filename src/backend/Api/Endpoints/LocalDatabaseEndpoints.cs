using ERP.Application.LocalData;
using ERP.Application.Common.Pagination;
using ERP.Application.Security;
using System.Text.Json;

namespace ERP.Api.Endpoints;

public static class LocalDatabaseEndpoints
{
    public static IEndpointRouteBuilder MapLocalDatabaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/local-database")
            .RequireAuthorization()
            .AddEndpointFilter<LocalDatabaseFeatureEndpointFilter>();

        group.MapPost("/backups", CreateBackupAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapGet("/backups/summary", BackupSummaryAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapGet("/backups", ListBackupsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapGet("/backups/{backupId}", BackupDetailsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapPost("/backups/{backupId}/verify", VerifyBackupAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapGet("/backup-retention", GetRetentionSettingsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapPut("/backup-retention", SaveRetentionSettingsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapPost("/upgrades", UpgradeAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapPost("/restore/validate", ValidateRestoreAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseRestoreValidate));

        group.MapPost("/restore", RestoreAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseRestoreExecute));

        group.MapGet("/diagnostics", DiagnosticsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapGet("/cloud/status", CloudStatusAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapGet("/cloud/configuration", GetCloudConfigurationAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapPut("/cloud/configuration", SaveCloudConfigurationAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapPost("/cloud/test-connection", TestCloudConnectionAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapGet("/cloud/backups", ListCloudBackupsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapGet("/cloud/sync", ListCombinedCloudBackupsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapPost("/cloud/backups/{backupId}/upload", UploadCloudBackupAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapPost("/cloud/uploads/{queueId}/retry", RetryCloudUploadAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapPost("/cloud/uploads/{queueId}/cancel", CancelCloudUploadAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapPost("/cloud/backups/{backupId}/download", DownloadCloudBackupAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseRestoreValidate));

        group.MapDelete("/cloud/backups/{backupId}", DeleteCloudBackupAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        return app;
    }

    private static async Task<IResult> CreateBackupAsync(
        HttpRequest request,
        IDatabaseBackupService backupService,
        CancellationToken cancellationToken)
    {
        var pathValidationResult = await RejectClientProvidedPathAsync(request, cancellationToken);
        if (pathValidationResult is not null)
        {
            return pathValidationResult;
        }

        var result = await backupService.CreateBackupAsync(BackupReason.Manual, cancellationToken);

        return result.Status == BackupStatus.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    }

    private static async Task<IResult?> RejectClientProvidedPathAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is null or 0)
        {
            return null;
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Results.BadRequest(new { message = "Manual backups do not accept request payloads." });
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (IsFilesystemPathProperty(property.Name))
                {
                    return Results.BadRequest(new { message = "Manual backups do not accept client-provided filesystem paths." });
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Manual backup request payload is invalid JSON." });
        }
    }

    private static bool IsFilesystemPathProperty(string propertyName)
        => string.Equals(propertyName, "path", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "filePath", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "backupPath", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "destination", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "destinationPath", StringComparison.OrdinalIgnoreCase);

    private static Task<IResult> BackupSummaryAsync(
        IBackupCatalogService backupCatalogService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await backupCatalogService.GetSummaryAsync(cancellationToken));

    private static Task<IResult> ListBackupsAsync(
        IBackupCatalogService backupCatalogService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await backupCatalogService.ListAsync(cancellationToken));

    private static Task<IResult> BackupDetailsAsync(
        string backupId,
        IBackupCatalogService backupCatalogService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await backupCatalogService.GetDetailsAsync(backupId, cancellationToken));

    private static Task<IResult> VerifyBackupAsync(
        string backupId,
        IBackupCatalogService backupCatalogService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await backupCatalogService.VerifyAsync(backupId, cancellationToken));

    private static Task<IResult> GetRetentionSettingsAsync(
        IBackupCatalogService backupCatalogService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await backupCatalogService.GetRetentionSettingsAsync(cancellationToken));

    private static Task<IResult> SaveRetentionSettingsAsync(
        BackupRetentionSettingsDto request,
        IBackupCatalogService backupCatalogService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await backupCatalogService.SaveRetentionSettingsAsync(request, cancellationToken));

    private static Task<IResult> UpgradeAsync(
        IDatabaseUpgradeService upgradeService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await upgradeService.UpgradeAsync(new DatabaseUpgradeRequest(), cancellationToken));

    private static Task<IResult> ValidateRestoreAsync(
        RestoreRequest request,
        IDatabaseRestoreService restoreService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await restoreService.CreatePreflightOperationAsync(request, cancellationToken));

    private static Task<IResult> RestoreAsync(
        RestoreRequest request,
        IDatabaseRestoreService restoreService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await restoreService.RestoreAsync(request, cancellationToken));

    private static Task<IResult> DiagnosticsAsync(
        IStartupDiagnosticsService diagnosticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await diagnosticsService.GetAsync(cancellationToken));

    private static Task<IResult> CloudStatusAsync(
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.GetStatusAsync(cancellationToken));

    private static Task<IResult> GetCloudConfigurationAsync(
        ICloudBackupConfigurationStore configurationStore,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await configurationStore.GetConfigurationAsync(cancellationToken));

    private static Task<IResult> SaveCloudConfigurationAsync(
        CloudBackupConfigurationRequest request,
        ICloudBackupConfigurationStore configurationStore,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await configurationStore.SaveConfigurationAsync(request, cancellationToken));

    private static Task<IResult> TestCloudConnectionAsync(
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.TestConnectionAsync(cancellationToken));

    private static Task<IResult> ListCloudBackupsAsync(
        int? page,
        int? pageSize,
        string? search,
        string? sortBy,
        SortDirection? sortDirection,
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.ListCloudBackupsAsync(
            new CloudBackupListQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection ?? SortDirection.Desc),
            cancellationToken));

    private static Task<IResult> ListCombinedCloudBackupsAsync(
        int? page,
        int? pageSize,
        string? search,
        string? sortBy,
        SortDirection? sortDirection,
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.ListCombinedBackupsAsync(
            new CloudBackupListQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection ?? SortDirection.Desc),
            cancellationToken));

    private static Task<IResult> UploadCloudBackupAsync(
        string backupId,
        CloudBackupUploadRequest? request,
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.EnqueueUploadAsync(backupId, request?.Force ?? false, cancellationToken));

    private static Task<IResult> RetryCloudUploadAsync(
        string queueId,
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.RetryUploadAsync(queueId, cancellationToken));

    private static Task<IResult> CancelCloudUploadAsync(
        string queueId,
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.CancelUploadAsync(queueId, cancellationToken));

    private static Task<IResult> DownloadCloudBackupAsync(
        string backupId,
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await cloudBackupService.DownloadAsync(backupId, cancellationToken));

    private static Task<IResult> DeleteCloudBackupAsync(
        string backupId,
        ICloudBackupWorkflowService cloudBackupService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            await cloudBackupService.DeleteCloudBackupAsync(backupId, cancellationToken);
            return new { message = "Cloud backup deleted." };
        });

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
