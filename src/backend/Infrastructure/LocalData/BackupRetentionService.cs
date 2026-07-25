using ERP.Application.LocalData;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.LocalData;

public sealed class BackupRetentionService(
    ILocalStoragePathService localStoragePathService,
    BackupManifestService manifestService,
    IOptions<BackupRetentionOptions> options) : IBackupRetentionService
{
    public async Task<BackupRetentionResult> ApplyAsync(
        IReadOnlyCollection<string> activeBackupIds,
        CancellationToken cancellationToken)
    {
        var retentionOptions = options.Value;
        var messages = new List<string>();
        if (!retentionOptions.Enabled)
        {
            return new BackupRetentionResult(false, 0, 0, ["Retention is disabled."]);
        }

        try
        {
            localStoragePathService.EnsureRequiredDirectories();
            var manifests = new List<(BackupManifest Manifest, string ManifestPath, string PayloadPath)>();
            foreach (var manifestPath in manifestService.EnumerateManifestPaths(localStoragePathService.BackupDirectory))
            {
                try
                {
                    var manifest = await manifestService.ReadAndValidateAsync(manifestPath, cancellationToken);
                    var payloadPath = Path.Combine(localStoragePathService.BackupDirectory, manifest.DatabaseFileName);
                    if (File.Exists(payloadPath))
                    {
                        manifests.Add((manifest, manifestPath, payloadPath));
                    }
                }
                catch (InvalidOperationException)
                {
                    messages.Add($"Preserved invalid manifest '{Path.GetFileName(manifestPath)}'.");
                }
            }

            var minimumAge = TimeSpan.FromHours(Math.Max(0, retentionOptions.MinimumAgeHoursBeforeDeletion));
            var utcNow = DateTime.UtcNow;
            var deletedPairs = 0;
            var preserved = 0;

            foreach (var group in manifests.GroupBy(item => item.Manifest.Reason))
            {
                var keepCount = GetKeepCount(group.Key, retentionOptions);
                var ordered = group.OrderByDescending(item => item.Manifest.CreatedAtUtc).ToList();
                var newest = ordered.FirstOrDefault();

                foreach (var item in ordered.Skip(keepCount))
                {
                    if (string.Equals(item.Manifest.BackupId, newest.Manifest?.BackupId, StringComparison.OrdinalIgnoreCase) ||
                        activeBackupIds.Contains(item.Manifest.BackupId, StringComparer.OrdinalIgnoreCase) ||
                        utcNow - item.Manifest.CreatedAtUtc < minimumAge)
                    {
                        preserved++;
                        continue;
                    }

                    File.Delete(item.PayloadPath);
                    File.Delete(item.ManifestPath);
                    deletedPairs++;
                }
            }

            return new BackupRetentionResult(true, deletedPairs, preserved, messages);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            messages.Add("Retention failed safely without affecting startup.");
            return new BackupRetentionResult(true, 0, 0, messages);
        }
    }

    private static int GetKeepCount(BackupReason reason, BackupRetentionOptions options)
        => Math.Max(1, reason switch
        {
            BackupReason.Manual => options.ManualCount,
            BackupReason.Scheduled => options.ScheduledCount,
            BackupReason.BeforeMigration => options.BeforeMigrationCount,
            BackupReason.BeforeRestore => options.BeforeRestoreCount,
            BackupReason.BeforeApplicationUpdate => options.BeforeApplicationUpdateCount,
            _ => 1
        });
}
