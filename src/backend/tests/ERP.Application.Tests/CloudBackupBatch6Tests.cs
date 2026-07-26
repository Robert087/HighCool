using ERP.Application.Common.Pagination;
using ERP.Application.LocalData;
using ERP.Infrastructure.LocalData;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.Application.Tests;

public sealed class CloudBackupBatch6Tests
{
    [Fact]
    public async Task CloudConfiguration_EncryptsCredentialsAndReturnsOnlyMaskedAccessKey()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var store = new CloudBackupConfigurationStore(
                storage,
                new DevelopmentFileBackupEncryptionKeyProvider(storage));

            var saved = await store.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
                true,
                true,
                "highcool-backups",
                "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
                "r2-access-key",
                "r2-secret-key",
                "tenant-a",
                25,
                12,
                4), CancellationToken.None);

            var configPath = Path.Combine(storage.DataDirectory, "cloud-backup-settings.json");
            var payload = await File.ReadAllTextAsync(configPath);
            var settings = await store.GetSettingsAsync(CancellationToken.None);

            Assert.True(saved.HasAccessKey);
            Assert.True(saved.HasSecretKey);
            Assert.Contains("r2-a", saved.AccessKey);
            Assert.Equal("r2-access-key", settings.AccessKey);
            Assert.Equal("r2-secret-key", settings.SecretKey);
            Assert.DoesNotContain("r2-access-key", payload);
            Assert.DoesNotContain("r2-secret-key", payload);
            Assert.Contains("cipherText", payload);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("http://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com")]
    [InlineData("https://user:password@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com")]
    [InlineData("https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com/backups")]
    [InlineData("https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com?x=1")]
    [InlineData("https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com#fragment")]
    [InlineData("https://localhost")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://169.254.169.254")]
    [InlineData("https://example.com")]
    [InlineData("https://not-an-account.r2.cloudflarestorage.com")]
    public async Task CloudConfiguration_RejectsUnsafeOrNonR2Endpoints(string endpoint)
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var store = new CloudBackupConfigurationStore(
                storage,
                new DevelopmentFileBackupEncryptionKeyProvider(storage));

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
                true,
                true,
                "highcool-backups",
                endpoint,
                "r2-access-key",
                "r2-secret-key",
                "tenant-a",
                25,
                12,
                4,
                CloudBackupCredentialUpdateMode.Replace), CancellationToken.None));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudConfiguration_PreservesAndClearsCredentialsExplicitly()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var store = new CloudBackupConfigurationStore(
                storage,
                new DevelopmentFileBackupEncryptionKeyProvider(storage));

            await store.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
                true,
                true,
                "highcool-backups",
                "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
                "r2-access-key",
                "r2-secret-key",
                "tenant-a",
                25,
                12,
                4,
                CloudBackupCredentialUpdateMode.Replace), CancellationToken.None);
            await store.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
                true,
                false,
                "highcool-backups",
                "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
                null,
                null,
                "tenant-b",
                10,
                20,
                2,
                CloudBackupCredentialUpdateMode.Preserve), CancellationToken.None);

            var preserved = await store.GetSettingsAsync(CancellationToken.None);
            Assert.Equal("r2-access-key", preserved.AccessKey);
            Assert.Equal("r2-secret-key", preserved.SecretKey);

            await store.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
                true,
                false,
                "highcool-backups",
                "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
                null,
                null,
                "tenant-b",
                10,
                20,
                2,
                CloudBackupCredentialUpdateMode.Clear), CancellationToken.None);

            var cleared = await store.GetSettingsAsync(CancellationToken.None);
            Assert.Equal("", cleared.AccessKey);
            Assert.Equal("", cleared.SecretKey);
            Assert.False(cleared.IsConfigured);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudConnectionTest_SucceedsWithConfiguredCredentialsUsingFakeProvider()
    {
        var root = CreateRoot();
        try
        {
            var workflow = await CreateConnectionTestWorkflowAsync(root, new FakeCloudBackupProvider());

            var result = await workflow.TestConnectionAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Succeeded);
            Assert.Equal(CloudBackupConnectionFailureCategory.None, result.Category);
            Assert.Equal(CloudBackupConnectionTestStage.Completed, result.Stage);
            Assert.True(result.CleanupSucceeded);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudConnectionTest_ReturnsMissingCredentialsWithoutCallingProvider()
    {
        var root = CreateRoot();
        try
        {
            var provider = new FakeCloudBackupProvider();
            var workflow = await CreateConnectionTestWorkflowAsync(
                root,
                provider,
                credentialUpdateMode: CloudBackupCredentialUpdateMode.Clear);

            var result = await workflow.TestConnectionAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CloudBackupConnectionFailureCategory.CredentialsMissing, result.Category);
            Assert.Equal(CloudBackupConnectionTestStage.Credentials, result.Stage);
            Assert.Equal(0, provider.TestConnectionCallCount);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudConnectionTest_ReturnsUnreadableCredentialsForIncompatibleEncryptedCredentials()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var store = new CloudBackupConfigurationStore(
                storage,
                new DevelopmentFileBackupEncryptionKeyProvider(storage));
            await store.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
                true,
                true,
                "highcool-backups",
                "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
                "access",
                "secret",
                "",
                10,
                10,
                3,
                CloudBackupCredentialUpdateMode.Replace), CancellationToken.None);
            var configPath = Path.Combine(storage.DataDirectory, "cloud-backup-settings.json");
            var payload = await File.ReadAllTextAsync(configPath);
            await File.WriteAllTextAsync(configPath, payload.Replace("\"cipherText\":", "\"cipherText\":\"not-valid-base64\", \"legacyCipherText\":"), CancellationToken.None);

            var workflow = CreateConnectionTestWorkflow(storage, store, new FakeCloudBackupProvider());

            var result = await workflow.TestConnectionAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CloudBackupConnectionFailureCategory.CredentialsUnreadable, result.Category);
            Assert.Equal(CloudBackupConnectionTestStage.Credentials, result.Stage);
            Assert.Contains("could not be decrypted", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData(CloudBackupConnectionFailureCategory.InvalidCredentials, CloudBackupConnectionTestStage.Write, 403, "SignatureDoesNotMatch")]
    [InlineData(CloudBackupConnectionFailureCategory.BucketNotFound, CloudBackupConnectionTestStage.Write, 404, "NoSuchBucket")]
    [InlineData(CloudBackupConnectionFailureCategory.WriteDenied, CloudBackupConnectionTestStage.Write, 403, "AccessDenied")]
    [InlineData(CloudBackupConnectionFailureCategory.ReadDenied, CloudBackupConnectionTestStage.Read, 403, "AccessDenied")]
    [InlineData(CloudBackupConnectionFailureCategory.DeleteDenied, CloudBackupConnectionTestStage.DeleteCleanup, 403, "AccessDenied")]
    [InlineData(CloudBackupConnectionFailureCategory.Timeout, CloudBackupConnectionTestStage.Write, null, null)]
    [InlineData(CloudBackupConnectionFailureCategory.DnsFailure, CloudBackupConnectionTestStage.ClientCreation, null, null)]
    [InlineData(CloudBackupConnectionFailureCategory.NetworkUnavailable, CloudBackupConnectionTestStage.Write, null, null)]
    [InlineData(CloudBackupConnectionFailureCategory.ContentVerificationFailed, CloudBackupConnectionTestStage.ChecksumVerification, null, null)]
    [InlineData(CloudBackupConnectionFailureCategory.CleanupFailed, CloudBackupConnectionTestStage.DeleteCleanup, 403, "AccessDenied")]
    public async Task CloudConnectionTest_ReturnsCategorizedSafeFailures(
        CloudBackupConnectionFailureCategory category,
        CloudBackupConnectionTestStage stage,
        int? statusCode,
        string? providerCode)
    {
        var root = CreateRoot();
        try
        {
            var provider = new FakeCloudBackupProvider
            {
                ConnectionFailure = new CloudBackupConnectionException(
                    category,
                    stage,
                    SafeConnectionMessage(category),
                    statusCode,
                    providerCode,
                    cleanupSucceeded: category != CloudBackupConnectionFailureCategory.CleanupFailed &&
                                      category != CloudBackupConnectionFailureCategory.DeleteDenied)
            };
            var workflow = await CreateConnectionTestWorkflowAsync(root, provider);

            var result = await workflow.TestConnectionAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(category, result.Category);
            Assert.Equal(stage, result.Stage);
            Assert.Equal(statusCode, result.StatusCode);
            Assert.Equal(providerCode, result.ProviderErrorCode);
            Assert.DoesNotContain("r2-test-secret-key", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("r2-test-access-key", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudConnectionTest_DoesNotExposeSecretsInResponseOrLogs()
    {
        var root = CreateRoot();
        try
        {
            var logger = new RecordingLogger<CloudBackupWorkflowService>();
            var provider = new FakeCloudBackupProvider
            {
                ConnectionFailure = new InvalidOperationException("raw secret should stay hidden")
            };
            var workflow = await CreateConnectionTestWorkflowAsync(root, provider, logger: logger);

            var result = await workflow.TestConnectionAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CloudBackupConnectionFailureCategory.UnknownProviderFailure, result.Category);
            Assert.DoesNotContain("secret", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("raw secret", string.Join('\n', logger.Messages), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudQueue_PersistsRetryStateWithBackoffAndMaxAttempts()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var queue = new CloudBackupQueueStore(storage);

            var enqueued = await queue.EnqueueAsync("backup001", 2, false, CancellationToken.None);
            var claimed = await queue.ClaimNextAsync(DateTime.UtcNow, CancellationToken.None);
            await queue.MarkFailedAsync(
                claimed!.QueueId,
                CloudBackupFailureCategory.TransientNetworkFailure,
                "Cloud backup is currently unavailable.",
                DateTime.UtcNow,
                TimeSpan.FromSeconds(30),
                CancellationToken.None);
            var afterFirstFailure = await queue.GetAsync(enqueued.QueueId, CancellationToken.None);

            Assert.Equal(CloudBackupUploadStatus.Queued, afterFirstFailure!.Status);
            Assert.NotNull(afterFirstFailure.NextAttemptAtUtc);

            var secondClaim = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(1), CancellationToken.None);
            await queue.MarkFailedAsync(
                secondClaim!.QueueId,
                CloudBackupFailureCategory.TransientNetworkFailure,
                "Cloud backup is currently unavailable.",
                DateTime.UtcNow,
                TimeSpan.FromSeconds(30),
                CancellationToken.None);
            var failed = await queue.GetAsync(enqueued.QueueId, CancellationToken.None);

            Assert.Equal(CloudBackupUploadStatus.Failed, failed!.Status);
            Assert.Equal(2, failed.Attempts);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudQueue_RecoversFromBackupAndThrowsWhenAllQueueFilesAreCorrupt()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var queue = new CloudBackupQueueStore(storage);
            var first = await queue.EnqueueAsync("backup001", 2, false, CancellationToken.None);
            _ = await queue.EnqueueAsync("backup002", 2, false, CancellationToken.None);
            var queuePath = Path.Combine(storage.DataDirectory, "cloud-backup-queue.json");
            var backupPath = Path.Combine(storage.DataDirectory, "cloud-backup-queue.json.bak");

            await File.WriteAllTextAsync(queuePath, "{", CancellationToken.None);
            var recovered = await new CloudBackupQueueStore(storage).ListAsync(CancellationToken.None);
            Assert.Single(recovered);
            Assert.Equal(first.BackupId, recovered.Single().BackupId);

            await File.WriteAllTextAsync(backupPath, "{", CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CloudBackupQueueStore(storage).ListAsync(CancellationToken.None));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudWorkflow_QueuesAndUploadsLocalBackupWithoutExposingSecrets()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            storage.EnsureRequiredDirectories();
            var manifestService = new BackupManifestService();
            var encryptionKeyProvider = new DevelopmentFileBackupEncryptionKeyProvider(storage);
            var manifestAuthenticationService = new BackupManifestAuthenticationService(encryptionKeyProvider);
            var unsignedManifest = new BackupManifest(
                BackupManifestService.CurrentManifestVersion,
                "backup002",
                "install-1",
                "1.0.0",
                5,
                DateTime.UtcNow,
                BackupReason.Manual,
                "HighCool_backup002.db.enc",
                5,
                "plain",
                "5994471abb01112afcc18159f6cc74b4f511b99806da59b3caf5a9c173cacfc5",
                new BackupEncryptionManifest("AES-256-GCM", "test", "key", Convert.ToBase64String(new byte[12]), Convert.ToBase64String(new byte[16])),
                5);
            var manifest = await manifestAuthenticationService.SignAsync(
                unsignedManifest,
                CloudflareR2BackupProvider.BuildExpectedPayloadKey("", unsignedManifest.BackupId, unsignedManifest.DatabaseFileName),
                CloudflareR2BackupProvider.BuildExpectedManifestKey("", unsignedManifest.BackupId, "HighCool_backup002.db.manifest.json"),
                CancellationToken.None);
            var payloadPath = Path.Combine(storage.BackupDirectory, manifest.DatabaseFileName);
            var manifestPath = manifestService.GetManifestPathForBackupFile(payloadPath);
            await File.WriteAllTextAsync(payloadPath, "12345");
            await manifestService.WriteAsync(manifestPath, manifest, CancellationToken.None);

            var configStore = new CloudBackupConfigurationStore(
                storage,
                encryptionKeyProvider);
            await configStore.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
                true,
                true,
                "highcool-backups",
                "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
                "access",
                "secret",
                "",
                10,
                10,
                3), CancellationToken.None);

            var provider = new FakeCloudBackupProvider();
            var queue = new CloudBackupQueueStore(storage);
            var workflow = new CloudBackupWorkflowService(
                configStore,
                provider,
                queue,
                new FakeBackupCatalogService([new BackupListItemDto(
                    manifest.BackupId,
                    manifest.CreatedAtUtc,
                    manifest.Reason,
                    BackupStatus.Succeeded,
                    manifest.DatabaseSizeBytes,
                    manifest.ApplicationVersion,
                    manifest.DatabaseSchemaVersion,
                    BackupIntegrityStatus.Verified,
                    manifest.CreatedAtUtc)]),
                storage,
                manifestService,
                manifestAuthenticationService,
                NullLogger<CloudBackupWorkflowService>.Instance);

            var queued = await workflow.EnqueueUploadAsync(manifest.BackupId, false, CancellationToken.None);
            await workflow.ProcessDueUploadsAsync(CancellationToken.None);
            var completed = await queue.GetAsync(queued.QueueId, CancellationToken.None);

            Assert.Equal(CloudBackupUploadStatus.Uploaded, completed!.Status);
            Assert.Equal(manifest.BackupId, provider.UploadedBackupIds.Single());
            Assert.DoesNotContain("secret", provider.LastMetadata, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CloudManifestAuthentication_RejectsTamperedManifest()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            storage.EnsureRequiredDirectories();
            var authentication = new BackupManifestAuthenticationService(new DevelopmentFileBackupEncryptionKeyProvider(storage));
            var manifest = new BackupManifest(
                BackupManifestService.CurrentManifestVersion,
                "backup003",
                "install-1",
                "1.0.0",
                5,
                DateTime.UtcNow,
                BackupReason.Manual,
                "HighCool_backup003.db.enc",
                5,
                "plain",
                "5994471abb01112afcc18159f6cc74b4f511b99806da59b3caf5a9c173cacfc5",
                new BackupEncryptionManifest("AES-256-GCM", "test", "key", Convert.ToBase64String(new byte[12]), Convert.ToBase64String(new byte[16])),
                5);
            var payloadKey = CloudflareR2BackupProvider.BuildExpectedPayloadKey("", manifest.BackupId, manifest.DatabaseFileName);
            var manifestKey = CloudflareR2BackupProvider.BuildExpectedManifestKey("", manifest.BackupId, "HighCool_backup003.db.manifest.json");
            var signed = await authentication.SignAsync(manifest, payloadKey, manifestKey, CancellationToken.None);

            await authentication.ValidateAsync(signed, payloadKey, manifestKey, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                authentication.ValidateAsync(signed with { EncryptedSha256 = new string('0', 64) }, payloadKey, manifestKey, CancellationToken.None));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), $"highcool-cloud-tests-{Guid.NewGuid():N}");

    private static LocalStoragePathService CreateStorage(string root)
        => new(
            Options.Create(new LocalStorageOptions
            {
                DataDirectory = Path.Combine(root, "Data"),
                BackupDirectory = Path.Combine(root, "Backups"),
                PendingBackupDirectory = Path.Combine(root, "Pending"),
                LogDirectory = Path.Combine(root, "Logs")
            }),
            new TestHostEnvironment(root));

    private static async Task<CloudBackupWorkflowService> CreateConnectionTestWorkflowAsync(
        string root,
        FakeCloudBackupProvider provider,
        CloudBackupCredentialUpdateMode credentialUpdateMode = CloudBackupCredentialUpdateMode.Replace,
        ILogger<CloudBackupWorkflowService>? logger = null)
    {
        var storage = CreateStorage(root);
        var store = new CloudBackupConfigurationStore(
            storage,
            new DevelopmentFileBackupEncryptionKeyProvider(storage));
        await store.SaveConfigurationAsync(new CloudBackupConfigurationRequest(
            true,
            true,
            "highcool-backups",
            "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
            credentialUpdateMode == CloudBackupCredentialUpdateMode.Replace ? "r2-test-access-key" : null,
            credentialUpdateMode == CloudBackupCredentialUpdateMode.Replace ? "r2-test-secret-key" : null,
            "",
            10,
            10,
            3,
            credentialUpdateMode), CancellationToken.None);
        return CreateConnectionTestWorkflow(storage, store, provider, logger);
    }

    private static CloudBackupWorkflowService CreateConnectionTestWorkflow(
        LocalStoragePathService storage,
        CloudBackupConfigurationStore store,
        FakeCloudBackupProvider provider,
        ILogger<CloudBackupWorkflowService>? logger = null)
    {
        var manifestService = new BackupManifestService();
        var encryptionKeyProvider = new DevelopmentFileBackupEncryptionKeyProvider(storage);
        return new CloudBackupWorkflowService(
            store,
            provider,
            new CloudBackupQueueStore(storage),
            new FakeBackupCatalogService([]),
            storage,
            manifestService,
            new BackupManifestAuthenticationService(encryptionKeyProvider),
            logger ?? NullLogger<CloudBackupWorkflowService>.Instance);
    }

    private static string SafeConnectionMessage(CloudBackupConnectionFailureCategory category)
        => category switch
        {
            CloudBackupConnectionFailureCategory.InvalidCredentials => "The R2 credentials are invalid.",
            CloudBackupConnectionFailureCategory.BucketNotFound => "The bucket was not found in this R2 account.",
            CloudBackupConnectionFailureCategory.WriteDenied => "The token does not have write access to this bucket.",
            CloudBackupConnectionFailureCategory.ReadDenied => "The token does not have read access to this bucket.",
            CloudBackupConnectionFailureCategory.DeleteDenied => "The token does not have delete access to this bucket.",
            CloudBackupConnectionFailureCategory.CleanupFailed => "The test object was written and read, but could not be deleted.",
            _ => "Cloud backup is currently unavailable."
        };

    private static void DeleteDirectory(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "HighCool.Tests";

        public string ContentRootPath { get; set; } = root;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class FakeCloudBackupProvider : ICloudBackupProvider
    {
        private readonly List<CloudBackupObject> _objects = [];
        private readonly Dictionary<string, string> _manifestPayloads = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _payloads = new(StringComparer.Ordinal);

        public List<string> UploadedBackupIds { get; } = [];

        public string LastMetadata { get; private set; } = "";

        public int TestConnectionCallCount { get; private set; }

        public Exception? ConnectionFailure { get; set; }

        public Task DeleteAsync(CloudBackupSettings settings, string backupId, CancellationToken cancellationToken)
        {
            _objects.RemoveAll(item => item.BackupId == backupId);
            return Task.CompletedTask;
        }

        public Task DownloadManifestAsync(CloudBackupSettings settings, string backupId, string manifestDestinationPath, CancellationToken cancellationToken)
        {
            var key = _objects.Single(item => string.Equals(item.BackupId, backupId, StringComparison.OrdinalIgnoreCase)).ManifestObjectKey!;
            File.Copy(_manifestPayloads[key], manifestDestinationPath, overwrite: true);
            return Task.CompletedTask;
        }

        public Task DownloadObjectAsync(CloudBackupSettings settings, string objectKey, string payloadDestinationPath, CancellationToken cancellationToken)
        {
            File.Copy(_payloads[objectKey], payloadDestinationPath, overwrite: true);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(CloudBackupSettings settings, string backupId, CancellationToken cancellationToken)
            => Task.FromResult(_objects.Any(item => item.BackupId == backupId));

        public Task<bool> ExistsObjectAsync(CloudBackupSettings settings, string objectKey, CancellationToken cancellationToken)
            => Task.FromResult(_payloads.ContainsKey(objectKey));

        public Task<IReadOnlyList<CloudBackupObject>> ListAsync(CloudBackupSettings settings, CloudBackupListQuery query, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CloudBackupObject>>(_objects);

        public Task<CloudBackupProviderConnectionTestResult> TestConnectionAsync(CloudBackupSettings settings, CancellationToken cancellationToken)
        {
            TestConnectionCallCount++;
            if (ConnectionFailure is not null)
            {
                return Task.FromException<CloudBackupProviderConnectionTestResult>(ConnectionFailure);
            }

            return Task.FromResult(new CloudBackupProviderConnectionTestResult());
        }

        public Task UploadAsync(CloudBackupSettings settings, BackupManifest manifest, string payloadPath, string manifestPath, CancellationToken cancellationToken)
        {
            UploadedBackupIds.Add(manifest.BackupId);
            LastMetadata = $"{manifest.BackupId}|{manifest.EncryptedSha256}|{settings.BucketName}";
            var payloadKey = CloudflareR2BackupProvider.BuildExpectedPayloadKey(settings.Prefix, manifest.BackupId, manifest.DatabaseFileName);
            var manifestKey = CloudflareR2BackupProvider.BuildExpectedManifestKey(settings.Prefix, manifest.BackupId, Path.GetFileName(manifestPath));
            _payloads[payloadKey] = payloadPath;
            _manifestPayloads[manifestKey] = manifestPath;
            _objects.Add(new CloudBackupObject(
                manifest.BackupId,
                manifestKey,
                manifest.CreatedAtUtc,
                manifest.EncryptedSizeBytes ?? manifest.DatabaseSizeBytes,
                manifest.EncryptedSha256,
                payloadKey,
                manifestKey));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBackupCatalogService(IReadOnlyList<BackupListItemDto> backups) : IBackupCatalogService
    {
        public Task<BackupCenterSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<BackupListItemDto>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(backups);

        public Task<BackupDetailsDto> GetDetailsAsync(string backupId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupIntegrityVerificationResultDto> VerifyAsync(string backupId, CancellationToken cancellationToken)
            => Task.FromResult(new BackupIntegrityVerificationResultDto(backupId, BackupIntegrityStatus.Verified, DateTime.UtcNow, "Verified."));

        public Task<BackupRetentionSettingsDto> GetRetentionSettingsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupRetentionSettingsDto> SaveRetentionSettingsAsync(BackupRetentionSettingsDto settings, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
