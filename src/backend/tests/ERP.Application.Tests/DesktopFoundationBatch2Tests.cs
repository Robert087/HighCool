using ERP.Application.LocalData;
using ERP.Domain.MasterData;
using ERP.Domain.System;
using ERP.Infrastructure.LocalData;
using ERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.Application.Tests;

public sealed class DesktopFoundationBatch2Tests
{
    [Fact]
    public async Task EncryptedBackup_WritesManifestAndRejectsTamperedPayload()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = CreateStorage(rootDirectory);
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolDatabaseAsync(databasePath);

            var backupService = CreateBackupService(databasePath, storage);
            var result = await backupService.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            Assert.Equal(BackupStatus.Succeeded, result.Status);
            var encryptedBackupPath = Directory.EnumerateFiles(storage.BackupDirectory, "HighCool_*.db.enc").Single();
            var manifestPath = Directory.EnumerateFiles(storage.BackupDirectory, "*.manifest.json").Single();

            Assert.DoesNotContain(Directory.EnumerateFiles(storage.BackupDirectory), path => path.EndsWith(".db", StringComparison.OrdinalIgnoreCase));

            var manifestService = new BackupManifestService();
            var manifest = await manifestService.ReadAndValidateAsync(manifestPath, CancellationToken.None);
            Assert.Equal(result.BackupId, manifest.BackupId);
            Assert.Equal("AES-256-GCM", manifest.Encryption.Algorithm);
            Assert.NotEqual(manifest.PlainSha256, manifest.EncryptedSha256);

            var restoreProbePath = Path.Combine(storage.PendingBackupDirectory, "probe.db");
            await backupService.DecryptBackupToTemporaryFileAsync(manifest, restoreProbePath, CancellationToken.None);
            Assert.Equal("ok", await SqliteDatabaseBackupService.RunIntegrityCheckAsync(restoreProbePath, CancellationToken.None));
            Assert.True(await SentinelExistsAsync(restoreProbePath));
            DeleteSqliteFileSet(restoreProbePath);

            await using (var stream = File.Open(encryptedBackupPath, FileMode.Open, FileAccess.ReadWrite))
            {
                stream.Position = Math.Max(0, stream.Length - 1);
                var value = stream.ReadByte();
                stream.Position = Math.Max(0, stream.Length - 1);
                stream.WriteByte((byte)(value ^ 0xFF));
            }

            await using var dbContext = CreateDbContext(databasePath);
            var restoreService = CreateRestoreService(databasePath, storage, dbContext);
            var preflight = await restoreService.ValidateAsync(new RestoreRequest(result.BackupId), CancellationToken.None);

            Assert.Equal(RestorePreflightStatus.ChecksumMismatch, preflight.Status);
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task RestorePreflight_ValidBackup_DoesNotReplaceLiveDatabase()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = CreateStorage(rootDirectory);
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolDatabaseAsync(databasePath, "LIVE");
            var originalChecksum = await SqliteDatabaseBackupService.CalculateChecksumAsync(databasePath, CancellationToken.None);

            var backupService = CreateBackupService(databasePath, storage);
            var backup = await backupService.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            await using var dbContext = CreateDbContext(databasePath);
            var restoreService = CreateRestoreService(databasePath, storage, dbContext);
            var preflight = await restoreService.ValidateAsync(new RestoreRequest(backup.BackupId), CancellationToken.None);
            var afterChecksum = await SqliteDatabaseBackupService.CalculateChecksumAsync(databasePath, CancellationToken.None);

            Assert.Equal(RestorePreflightStatus.Valid, preflight.Status);
            Assert.Equal(DatabaseSchemaInfo.CurrentSchemaVersion, preflight.SchemaVersion);
            Assert.Equal(originalChecksum, afterChecksum);
            Assert.Empty(Directory.EnumerateFiles(storage.PendingBackupDirectory));
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task RestoreExecution_ReplacesLiveDatabaseOnlyAfterSafetyBackup()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = CreateStorage(rootDirectory);
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolDatabaseAsync(databasePath, "B2S");
            var backupService = CreateBackupService(databasePath, storage);
            var backup = await backupService.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            await AddSentinelAsync(databasePath, "AFTER_BACKUP");
            Assert.True(await SentinelExistsAsync(databasePath, "AFTER_BACKUP"));

            await using var dbContext = CreateDbContext(databasePath);
            var restoreService = CreateRestoreService(databasePath, storage, dbContext);
            var preflight = await restoreService.CreatePreflightOperationAsync(new RestoreRequest(backup.BackupId), CancellationToken.None);
            var result = await restoreService.RestoreAsync(
                new RestoreRequest(backup.BackupId, DatabaseRestoreService.RequiredConfirmation, preflight.OperationId),
                CancellationToken.None);

            Assert.Equal(RestoreStatus.Completed, result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.SafetyBackupId));
            Assert.True(await SentinelExistsAsync(databasePath, "B2S"));
            Assert.False(await SentinelExistsAsync(databasePath, "AFTER_BACKUP"));

            var manifests = Directory.EnumerateFiles(storage.BackupDirectory, "*.manifest.json").ToList();
            Assert.Equal(2, manifests.Count);
            var reasons = new List<BackupReason>();
            var manifestService = new BackupManifestService();
            foreach (var manifestPath in manifests)
            {
                reasons.Add((await manifestService.ReadAndValidateAsync(manifestPath, CancellationToken.None)).Reason);
            }

            Assert.Contains(BackupReason.Manual, reasons);
            Assert.Contains(BackupReason.BeforeRestore, reasons);
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task BackupRetention_KeepsNewestAndInvalidManifestWhileDeletingExpiredOlderPairs()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = CreateStorage(rootDirectory);
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolDatabaseAsync(databasePath);
            var backupService = CreateBackupService(databasePath, storage);
            var manifestService = new BackupManifestService();

            var older = await backupService.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);
            var newer = await backupService.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);
            await AgeManifestAsync(storage, manifestService, older.BackupId, DateTime.UtcNow.AddDays(-10));
            await AgeManifestAsync(storage, manifestService, newer.BackupId, DateTime.UtcNow.AddDays(-1));

            var invalidManifestPath = Path.Combine(storage.BackupDirectory, "invalid.manifest.json");
            await File.WriteAllTextAsync(invalidManifestPath, "{not-json", CancellationToken.None);

            var retention = new BackupRetentionService(
                storage,
                manifestService,
                CreateCatalogService(
                    databasePath,
                    storage,
                    Options.Create(new BackupRetentionOptions
                    {
                        Enabled = true,
                        ManualCount = 1,
                        MinimumAgeHoursBeforeDeletion = 0
                    })),
                new LocalDatabaseOperationCoordinator());

            var result = await retention.ApplyAsync([], CancellationToken.None);

            Assert.True(result.Enabled);
            Assert.Equal(1, result.DeletedPairs);
            Assert.Contains(result.Messages, message => message.Contains("Preserved invalid manifest", StringComparison.Ordinal));
            Assert.Contains(Directory.EnumerateFiles(storage.BackupDirectory, "*.manifest.json"), path => Path.GetFileName(path) == "invalid.manifest.json");

            var remainingManifests = Directory.EnumerateFiles(storage.BackupDirectory, "HighCool_*.manifest.json").ToList();
            Assert.Single(remainingManifests);
            var remaining = await manifestService.ReadAndValidateAsync(remainingManifests.Single(), CancellationToken.None);
            Assert.Equal(newer.BackupId, remaining.BackupId);
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task BackupCatalog_ListsDetailsAndPersistsVerificationStatus()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = CreateStorage(rootDirectory);
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolDatabaseAsync(databasePath);
            var backup = await CreateBackupService(databasePath, storage).CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            var catalog = CreateCatalogService(databasePath, storage, Options.Create(new BackupRetentionOptions()));

            var initialList = await catalog.ListAsync(CancellationToken.None);
            var initialItem = Assert.Single(initialList);
            Assert.Equal(backup.BackupId, initialItem.BackupId);
            Assert.Equal(BackupIntegrityStatus.Unknown, initialItem.IntegrityStatus);

            var verification = await catalog.VerifyAsync(backup.BackupId, CancellationToken.None);
            Assert.Equal(BackupIntegrityStatus.Verified, verification.Status);

            var refreshedList = await catalog.ListAsync(CancellationToken.None);
            Assert.Equal(BackupIntegrityStatus.Verified, Assert.Single(refreshedList).IntegrityStatus);

            var details = await catalog.GetDetailsAsync(backup.BackupId, CancellationToken.None);
            Assert.Equal(backup.BackupId, details.BackupId);
            Assert.Equal("AES-256-GCM", details.EncryptionAlgorithm);
            Assert.Equal(RestorePreflightStatus.Valid, details.RestoreCompatibilityStatus);
            Assert.DoesNotContain(storage.BackupDirectory, details.DatabaseFileName, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task BackupCatalog_SaveRetentionSettings_ClampsAndPersistsSafeValues()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = CreateStorage(rootDirectory);
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolDatabaseAsync(databasePath);
            var catalog = CreateCatalogService(databasePath, storage, Options.Create(new BackupRetentionOptions()));

            var saved = await catalog.SaveRetentionSettingsAsync(
                new BackupRetentionSettingsDto(
                    true,
                    ManualCount: 0,
                    ScheduledCount: 500,
                    BeforeMigrationCount: 2,
                    BeforeRestoreCount: 3,
                    BeforeApplicationUpdateCount: 4,
                    MinimumAgeHoursBeforeDeletion: 9000),
                CancellationToken.None);

            Assert.Equal(1, saved.ManualCount);
            Assert.Equal(365, saved.ScheduledCount);
            Assert.Equal(8760, saved.MinimumAgeHoursBeforeDeletion);

            var reloaded = await catalog.GetRetentionSettingsAsync(CancellationToken.None);
            Assert.Equal(saved, reloaded);
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task StartupDiagnostics_ReturnsSafeContractWithBackupAndJournalState()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = CreateStorage(rootDirectory);
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolDatabaseAsync(databasePath);
            var backup = await CreateBackupService(databasePath, storage).CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            await using var dbContext = CreateDbContext(databasePath);
            dbContext.ApplicationDatabaseUpgradeJournal.Add(new ApplicationDatabaseUpgradeJournal
            {
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CompletedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                FromSchemaVersion = 1,
                TargetSchemaVersion = DatabaseSchemaInfo.CurrentSchemaVersion,
                PreUpgradeBackupId = backup.BackupId,
                Status = DatabaseUpgradeJournalStatus.Completed,
                ApplicationVersion = "test",
                InstallationId = "test",
                CreatedBy = "test"
            });
            await dbContext.SaveChangesAsync();

            var metadataService = new ApplicationDatabaseMetadataService(dbContext);
            var diagnostics = new StartupDiagnosticsService(
                CreateDatabaseConfigurationService(databasePath, storage),
                new DatabaseHealthService(dbContext, CreateDatabaseConfigurationService(databasePath, storage), metadataService),
                storage,
                new BackupManifestService(),
                dbContext);

            var result = await diagnostics.GetAsync(CancellationToken.None);

            Assert.Equal(StartupDiagnosticStatus.Healthy, result.Status);
            Assert.False(result.RetryAllowed);
            Assert.True(result.BackupAvailable);
            Assert.True(result.RestoreAvailable);
            Assert.Equal(DatabaseProviderNames.Sqlite, result.DatabaseProvider);
            Assert.Equal(DatabaseSchemaInfo.CurrentSchemaVersion, result.SchemaVersion);
            Assert.Equal(DatabaseUpgradeJournalStatus.Completed.ToString(), result.LastUpgradeStatus);
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    private static async Task AgeManifestAsync(
        TestLocalStoragePathService storage,
        BackupManifestService manifestService,
        string backupId,
        DateTime createdAtUtc)
    {
        foreach (var manifestPath in Directory.EnumerateFiles(storage.BackupDirectory, "*.manifest.json"))
        {
            var manifest = await manifestService.ReadAndValidateAsync(manifestPath, CancellationToken.None);
            if (manifest.BackupId == backupId)
            {
                await manifestService.WriteAsync(manifestPath, manifest with { CreatedAtUtc = createdAtUtc }, CancellationToken.None);
                return;
            }
        }

        throw new InvalidOperationException("Manifest not found for test backup.");
    }

    private static DatabaseRestoreService CreateRestoreService(
        string databasePath,
        ILocalStoragePathService storage,
        AppDbContext dbContext)
    {
        var configurationService = CreateDatabaseConfigurationService(databasePath, storage);
        var metadataService = new ApplicationDatabaseMetadataService(dbContext);
        var backupService = CreateBackupService(databasePath, storage);
        var operationCoordinator = new LocalDatabaseOperationCoordinator();
        var executionContext = new TestRequestExecutionContext { UserId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), MembershipId = Guid.NewGuid(), SessionId = Guid.NewGuid() };
        return new DatabaseRestoreService(
            dbContext,
            configurationService,
            storage,
            metadataService,
            backupService,
            backupService,
            new BackupManifestService(),
            operationCoordinator,
            new InMemoryRestorePreflightOperationStore(Options.Create(new RestorePreflightOperationOptions())),
            executionContext);
    }

    private static BackupCatalogService CreateCatalogService(
        string databasePath,
        ILocalStoragePathService storage,
        IOptions<BackupRetentionOptions> retentionOptions)
    {
        var dbContext = CreateDbContext(databasePath);
        return new BackupCatalogService(
            CreateDatabaseConfigurationService(databasePath, storage),
            storage,
            CreateBackupService(databasePath, storage),
            new BackupManifestService(),
            CreateRestoreService(databasePath, storage, dbContext),
            retentionOptions);
    }

    private static SqliteDatabaseBackupService CreateBackupService(
        string databasePath,
        ILocalStoragePathService storage)
    {
        var dbContext = CreateDbContext(databasePath);
        return new SqliteDatabaseBackupService(
            CreateDatabaseConfigurationService(databasePath, storage),
            storage,
            new ApplicationDatabaseMetadataService(dbContext),
            new DevelopmentFileBackupEncryptionKeyProvider(storage),
            new BackupManifestService(),
            new BackupManifestAuthenticationService(new DevelopmentFileBackupEncryptionKeyProvider(storage)),
            new LocalDatabaseOperationCoordinator());
    }

    private static TestLocalStoragePathService CreateStorage(string rootDirectory)
        => new(
            Path.Combine(rootDirectory, "Data"),
            Path.Combine(rootDirectory, "Backups"),
            Path.Combine(rootDirectory, "Pending"),
            Path.Combine(rootDirectory, "Logs"));

    private static async Task CreateHighCoolDatabaseAsync(string databasePath, string sentinelCode = "B2S")
    {
        await using var dbContext = CreateDbContext(databasePath);
        await dbContext.Database.EnsureCreatedAsync();

        var metadataService = new ApplicationDatabaseMetadataService(dbContext);
        await metadataService.EnsureInitializedAsync(CancellationToken.None);

        dbContext.Uoms.Add(new Uom
        {
            Code = sentinelCode,
            Name = "Batch 2 Sentinel",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "test"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task AddSentinelAsync(string databasePath, string code)
    {
        await using var dbContext = CreateDbContext(databasePath);
        dbContext.Uoms.Add(new Uom
        {
            Code = code,
            Name = code,
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "test"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<bool> SentinelExistsAsync(string databasePath, string code = "B2S")
    {
        await using var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(databasePath));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM uoms WHERE code = $code;";
        command.Parameters.AddWithValue("$code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static AppDbContext CreateDbContext(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(SqliteTestDatabase.CreateConnectionString(databasePath))
            .Options;

        return new AppDbContext(options);
    }

    private static DatabaseConfigurationService CreateDatabaseConfigurationService(
        string databasePath,
        ILocalStoragePathService storage)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:SqliteFileName"] = Path.GetFileName(databasePath)
            })
            .Build();

        return new DatabaseConfigurationService(configuration, storage);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.db");
    }

    private static void DeleteSqliteFileSet(string databasePath)
    {
        SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
    }

    private static void DeleteDirectoryIfExists(string? directoryPath)
    {
        SqliteTestDatabase.DeleteDirectoryIfExists(directoryPath);
    }

    private sealed class TestLocalStoragePathService(
        string dataDirectory,
        string backupDirectory,
        string pendingBackupDirectory,
        string logDirectory) : ILocalStoragePathService
    {
        public string DataDirectory { get; } = dataDirectory;

        public string BackupDirectory { get; } = backupDirectory;

        public string PendingBackupDirectory { get; } = pendingBackupDirectory;

        public string LogDirectory { get; } = logDirectory;

        public string GetSqliteDatabasePath(string fileName) => Path.Combine(DataDirectory, fileName);

        public void EnsureRequiredDirectories()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(PendingBackupDirectory);
            Directory.CreateDirectory(LogDirectory);
        }
    }
}
