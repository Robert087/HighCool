using ERP.Application.LocalData;

namespace ERP.Infrastructure.LocalData;

public sealed class CloudAwareDatabaseBackupService(
    SqliteDatabaseBackupService localBackupService,
    ICloudBackupConfigurationStore configurationStore,
    ICloudBackupQueueStore queueStore) : IDatabaseBackupService
{
    public async Task<BackupResult> CreateBackupAsync(
        BackupReason reason,
        CancellationToken cancellationToken)
    {
        var result = await localBackupService.CreateBackupAsync(reason, cancellationToken);
        if (result.Status != BackupStatus.Succeeded)
        {
            return result;
        }

        try
        {
            var settings = await configurationStore.GetSettingsAsync(cancellationToken);
            if (settings.Enabled && settings.AutoUploadAfterBackup && settings.IsConfigured)
            {
                await queueStore.EnqueueAsync(result.BackupId, settings.RetryCount, false, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            // Local backup must remain successful even when cloud queue persistence is temporarily unavailable.
        }

        return result;
    }
}
