using ERP.Application.LocalData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.LocalData;

public sealed class StartupDiagnosticsService(
    IDatabaseConfigurationService databaseConfigurationService,
    IDatabaseHealthService healthService,
    ILocalStoragePathService localStoragePathService,
    BackupManifestService manifestService,
    AppDbContext dbContext) : IStartupDiagnosticsService
{
    public async Task<StartupDiagnosticResult> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configuration = databaseConfigurationService.GetConfiguration();
            var health = await healthService.CheckAsync(requireWritable: false, cancellationToken);
            var status = health.Status switch
            {
                DatabaseHealthStatus.Healthy => StartupDiagnosticStatus.Healthy,
                DatabaseHealthStatus.Missing => StartupDiagnosticStatus.DatabaseMissing,
                DatabaseHealthStatus.Unavailable => StartupDiagnosticStatus.DatabaseUnavailable,
                DatabaseHealthStatus.Corrupt => StartupDiagnosticStatus.DatabaseCorrupt,
                DatabaseHealthStatus.UnsupportedSchema => StartupDiagnosticStatus.UnsupportedSchema,
                DatabaseHealthStatus.ReadOnly => StartupDiagnosticStatus.ReadOnly,
                _ => StartupDiagnosticStatus.ConfigurationInvalid
            };

            var lastBackup = await GetLastBackupTimeAsync(cancellationToken);
            var lastUpgradeStatus = await dbContext.ApplicationDatabaseUpgradeJournal
                .IgnoreQueryFilters()
                .OrderByDescending(entity => entity.StartedAtUtc)
                .Select(entity => entity.Status.ToString())
                .FirstOrDefaultAsync(cancellationToken);
            var lastRestoreStatus = await dbContext.ApplicationDatabaseRestoreJournal
                .IgnoreQueryFilters()
                .OrderByDescending(entity => entity.StartedAtUtc)
                .Select(entity => entity.Status.ToString())
                .FirstOrDefaultAsync(cancellationToken);

            return new StartupDiagnosticResult(
                status,
                status == StartupDiagnosticStatus.Healthy ? "HighCool is ready" : "HighCool needs attention",
                health.Message,
                $"HC-{status}",
                RetryAllowed: status != StartupDiagnosticStatus.Healthy,
                BackupAvailable: lastBackup.HasValue,
                RestoreAvailable: lastBackup.HasValue,
                DateTime.UtcNow,
                configuration.Provider,
                health.SchemaVersion,
                health.ApplicationVersion,
                lastBackup,
                lastUpgradeStatus,
                lastRestoreStatus);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new StartupDiagnosticResult(
                StartupDiagnosticStatus.ConfigurationInvalid,
                "HighCool configuration is invalid",
                "Startup diagnostics could not read local configuration safely.",
                "HC-ConfigurationInvalid",
                RetryAllowed: true,
                BackupAvailable: false,
                RestoreAvailable: false,
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                null,
                null);
        }
    }

    private async Task<DateTime?> GetLastBackupTimeAsync(CancellationToken cancellationToken)
    {
        DateTime? last = null;
        foreach (var manifestPath in manifestService.EnumerateManifestPaths(localStoragePathService.BackupDirectory))
        {
            try
            {
                var manifest = await manifestService.ReadAndValidateAsync(manifestPath, cancellationToken);
                if (last is null || manifest.CreatedAtUtc > last)
                {
                    last = manifest.CreatedAtUtc;
                }
            }
            catch (InvalidOperationException)
            {
                // Invalid backup metadata is intentionally omitted from safe startup diagnostics.
            }
        }

        return last;
    }
}
