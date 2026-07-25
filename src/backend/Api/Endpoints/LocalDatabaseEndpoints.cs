using ERP.Application.LocalData;
using ERP.Application.Security;
using System.Text.Json;

namespace ERP.Api.Endpoints;

public static class LocalDatabaseEndpoints
{
    public static IEndpointRouteBuilder MapLocalDatabaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/local-database")
            .RequireAuthorization();

        group.MapPost("/backups", CreateBackupAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseBackupCreate));

        group.MapPost("/upgrades", UpgradeAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

        group.MapPost("/restore/validate", ValidateRestoreAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseRestoreValidate));

        group.MapPost("/restore", RestoreAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseRestoreExecute));

        group.MapGet("/diagnostics", DiagnosticsAsync)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SettingsDatabaseDiagnosticsRead));

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

    private static Task<IResult> UpgradeAsync(
        IDatabaseUpgradeService upgradeService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await upgradeService.UpgradeAsync(new DatabaseUpgradeRequest(), cancellationToken));

    private static Task<IResult> ValidateRestoreAsync(
        RestoreRequest request,
        IDatabaseRestoreService restoreService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await restoreService.ValidateAsync(request, cancellationToken));

    private static Task<IResult> RestoreAsync(
        RestoreRequest request,
        IDatabaseRestoreService restoreService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await restoreService.RestoreAsync(request, cancellationToken));

    private static Task<IResult> DiagnosticsAsync(
        IStartupDiagnosticsService diagnosticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(async () => await diagnosticsService.GetAsync(cancellationToken));

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
