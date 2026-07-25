using ERP.Application.LocalData;
using ERP.Domain.MasterData;
using ERP.Infrastructure.LocalData;
using ERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.Application.Tests;

public sealed class DesktopFoundationBatch1Tests
{
    [Fact]
    public void LocalStoragePathService_DefaultProductionPath_ResolvesOutsideInstallDirectory()
    {
        var installDirectory = CreateTempDirectory();
        var service = new LocalStoragePathService(
            Options.Create(new LocalStorageOptions()),
            new TestHostEnvironment
            {
                EnvironmentName = Environments.Production,
                ContentRootPath = installDirectory
            });

        Assert.False(IsSameOrChildPath(installDirectory, service.DataDirectory));
    }

    [Fact]
    public void LocalStoragePathService_ConfiguredRelativeOverride_IsDeterministicAndCreatesDirectories()
    {
        var contentRoot = CreateTempDirectory();
        var options = new LocalStorageOptions
        {
            DataDirectory = "dev-data",
            BackupDirectory = "dev-backups",
            PendingBackupDirectory = "dev-pending",
            LogDirectory = "dev-logs"
        };

        var service = new LocalStoragePathService(
            Options.Create(options),
            new TestHostEnvironment { EnvironmentName = Environments.Development, ContentRootPath = contentRoot });

        Assert.Equal(Path.Combine(contentRoot, "dev-data"), service.DataDirectory);

        service.EnsureRequiredDirectories();

        Assert.True(Directory.Exists(service.DataDirectory));
        Assert.True(Directory.Exists(service.BackupDirectory));
        Assert.True(Directory.Exists(service.PendingBackupDirectory));
        Assert.True(Directory.Exists(service.LogDirectory));
    }

    [Fact]
    public void LocalStoragePathService_ExistingFilePath_FailsWithoutOverwritingFile()
    {
        var contentRoot = CreateTempDirectory();
        var filePath = Path.Combine(contentRoot, "not-a-directory");
        File.WriteAllText(filePath, "sentinel");

        var service = new LocalStoragePathService(
            Options.Create(new LocalStorageOptions { DataDirectory = filePath }),
            new TestHostEnvironment { EnvironmentName = Environments.Development, ContentRootPath = contentRoot });

        var exception = Assert.Throws<InvalidOperationException>(() => service.EnsureRequiredDirectories());

        Assert.Contains("points to a file", exception.Message);
        Assert.Equal("sentinel", File.ReadAllText(filePath));
    }

    [Fact]
    public void DatabaseConfigurationService_Sqlite_UsesResolvedAbsoluteDataPath()
    {
        var dataDirectory = CreateTempDirectory();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:SqliteFileName"] = "highcool-test.db"
        });

        var service = new DatabaseConfigurationService(configuration, new TestLocalStoragePathService(dataDirectory));

        var result = service.GetConfiguration();

        Assert.Equal(DatabaseProviderNames.Sqlite, result.Provider);
        var sqliteDatabasePath = Assert.IsType<string>(result.SqliteDatabasePath);
        Assert.Equal(Path.Combine(dataDirectory, "highcool-test.db"), sqliteDatabasePath);
        Assert.Contains(sqliteDatabasePath, result.ConnectionString);
    }

    [Fact]
    public void DatabaseConfigurationService_SqlServer_UsesConfiguredConnectionString()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SqlServer",
            ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=HighCool;Trusted_Connection=True"
        });

        var service = new DatabaseConfigurationService(configuration, new TestLocalStoragePathService(CreateTempDirectory()));

        var result = service.GetConfiguration();

        Assert.Equal(DatabaseProviderNames.SqlServer, result.Provider);
        Assert.Equal("Server=.;Database=HighCool;Trusted_Connection=True", result.ConnectionString);
        Assert.Null(result.SqliteDatabasePath);
    }

    [Theory]
    [InlineData("Postgres")]
    [InlineData("")]
    public void DatabaseConfigurationService_UnsupportedProvider_FailsClearly(string provider)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider
        });

        var service = new DatabaseConfigurationService(configuration, new TestLocalStoragePathService(CreateTempDirectory()));

        Assert.Throws<InvalidOperationException>(() => service.GetConfiguration());
    }

    [Fact]
    public void DatabaseConfigurationService_MissingSqlServerConnectionString_FailsClearly()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SqlServer"
        });

        var service = new DatabaseConfigurationService(configuration, new TestLocalStoragePathService(CreateTempDirectory()));

        var exception = Assert.Throws<InvalidOperationException>(() => service.GetConfiguration());
        Assert.Contains("DefaultConnection", exception.Message);
    }

    [Fact]
    public async Task MetadataService_NewAndRepeatStartup_PreservesMetadata()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await using var dbContext = CreateDbContext(databasePath);
            await dbContext.Database.EnsureCreatedAsync();
            var service = new ApplicationDatabaseMetadataService(dbContext);

            var first = await service.EnsureInitializedAsync(CancellationToken.None);
            var second = await service.EnsureInitializedAsync(CancellationToken.None);

            Assert.Equal(DatabaseSchemaInfo.CurrentSchemaVersion, first.DatabaseSchemaVersion);
            Assert.Equal(first.DatabaseCreatedAtUtc, second.DatabaseCreatedAtUtc);
            Assert.Equal(first.DatabaseSchemaVersion, second.DatabaseSchemaVersion);
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task MetadataService_ExistingDatabaseWithoutMetadata_ReceivesMetadataSafely()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await using var dbContext = CreateDbContext(databasePath);
            await dbContext.Database.EnsureCreatedAsync();

            await using (var command = dbContext.Database.GetDbConnection().CreateCommand())
            {
                await dbContext.Database.OpenConnectionAsync();
                command.CommandText = "DROP TABLE application_database_metadata;";
                await command.ExecuteNonQueryAsync();
            }

            var service = new ApplicationDatabaseMetadataService(dbContext);
            var metadata = await service.EnsureInitializedAsync(CancellationToken.None);

            Assert.Equal(DatabaseSchemaInfo.CurrentSchemaVersion, metadata.DatabaseSchemaVersion);
            Assert.True(await dbContext.ApplicationDatabaseMetadata.IgnoreQueryFilters().AnyAsync());
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task MetadataService_NewerUnsupportedSchema_Fails()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await using var dbContext = CreateDbContext(databasePath);
            await dbContext.Database.EnsureCreatedAsync();
            var service = new ApplicationDatabaseMetadataService(dbContext);
            await service.EnsureInitializedAsync(CancellationToken.None);

            var metadata = await dbContext.ApplicationDatabaseMetadata.IgnoreQueryFilters().SingleAsync();
            metadata.DatabaseSchemaVersion = DatabaseSchemaInfo.CurrentSchemaVersion + 1;
            await dbContext.SaveChangesAsync();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureInitializedAsync(CancellationToken.None));
            Assert.Contains("newer", exception.Message);
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task DatabaseHealthService_HealthySqliteDatabase_ReturnsHealthy()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await using var dbContext = CreateDbContext(databasePath);
            await dbContext.Database.EnsureCreatedAsync();
            var metadataService = new ApplicationDatabaseMetadataService(dbContext);
            await metadataService.EnsureInitializedAsync(CancellationToken.None);

            var healthService = new DatabaseHealthService(
                dbContext,
                CreateDatabaseConfigurationService(databasePath),
                metadataService);

            var result = await healthService.CheckAsync(requireWritable: true, CancellationToken.None);

            Assert.Equal(DatabaseHealthStatus.Healthy, result.Status);
            Assert.Equal(DatabaseSchemaInfo.CurrentSchemaVersion, result.SchemaVersion);
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task DatabaseHealthService_MissingDatabase_ReturnsMissing()
    {
        var databasePath = CreateDatabasePath();
        await using var dbContext = CreateDbContext(databasePath);
        var metadataService = new ApplicationDatabaseMetadataService(dbContext);

        var healthService = new DatabaseHealthService(
            dbContext,
            CreateDatabaseConfigurationService(databasePath),
            metadataService);

        var result = await healthService.CheckAsync(requireWritable: false, CancellationToken.None);

        Assert.Equal(DatabaseHealthStatus.Missing, result.Status);
    }

    [Fact]
    public async Task SqliteBackupService_CreatesVerifiedBackupWhileSourceIsOpen()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = new TestLocalStoragePathService(
            Path.Combine(rootDirectory, "Data"),
            Path.Combine(rootDirectory, "Backups"),
            Path.Combine(rootDirectory, "PendingBackups"),
            Path.Combine(rootDirectory, "Logs"));
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolSentinelDatabaseAsync(databasePath);

            await using var heldOpenConnection = new SqliteConnection($"Data Source={databasePath}");
            await heldOpenConnection.OpenAsync();

            var service = CreateBackupService(databasePath, storage);

            var result = await service.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            Assert.Equal(BackupStatus.Succeeded, result.Status);
            Assert.True(result.SizeBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(result.ChecksumSha256));

            var backupFile = Directory.EnumerateFiles(storage.BackupDirectory, "HighCool_*.db.enc").Single();
            var manifestFile = Directory.EnumerateFiles(storage.BackupDirectory, "*.manifest.json").Single();
            Assert.Equal(Path.GetFileName(backupFile), result.BackupFileName);
            Assert.Equal(Path.GetFileName(manifestFile), result.ManifestFileName);
            var manifest = await new BackupManifestService().ReadAndValidateAsync(manifestFile, CancellationToken.None);
            var restoredBackupPath = Path.Combine(storage.PendingBackupDirectory, "restored.db");
            await service.DecryptBackupToTemporaryFileAsync(manifest, restoredBackupPath, CancellationToken.None);
            Assert.Equal("ok", await RunIntegrityCheckAsync(restoredBackupPath));
            Assert.True(await SentinelExistsAsync(restoredBackupPath));
            DeleteSqliteFileSet(restoredBackupPath);
            Assert.Empty(Directory.EnumerateFiles(storage.PendingBackupDirectory));
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SqliteBackupService_FailedBackup_LeavesNoFinalIncompleteFile()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = new TestLocalStoragePathService(rootDirectory, rootDirectory, Path.Combine(rootDirectory, "Pending"), Path.Combine(rootDirectory, "Logs"));

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolSentinelDatabaseAsync(databasePath);

            var service = CreateBackupService(databasePath, storage);

            var result = await service.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            Assert.Equal(BackupStatus.Failed, result.Status);
            Assert.Empty(Directory.EnumerateFiles(rootDirectory, "HighCool_*.db.enc"));
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SqliteBackupService_FileNamesAreUniqueAndExistingBackupsAreNotOverwritten()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = new TestLocalStoragePathService(
            Path.Combine(rootDirectory, "Data"),
            Path.Combine(rootDirectory, "Backups"),
            Path.Combine(rootDirectory, "Pending"),
            Path.Combine(rootDirectory, "Logs"));
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolSentinelDatabaseAsync(databasePath);

            var service = CreateBackupService(databasePath, storage);

            var first = await service.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);
            var second = await service.CreateBackupAsync(BackupReason.Manual, CancellationToken.None);

            Assert.Equal(BackupStatus.Succeeded, first.Status);
            Assert.Equal(BackupStatus.Succeeded, second.Status);
            Assert.NotEqual(first.BackupId, second.BackupId);
            Assert.Equal(2, Directory.EnumerateFiles(storage.BackupDirectory, "HighCool_*.db.enc").Count());
            Assert.Equal(2, Directory.EnumerateFiles(storage.BackupDirectory, "*.manifest.json").Count());
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SqliteBackupService_CanceledBeforeStart_ReturnsCanceledWithoutFinalFile()
    {
        var databasePath = CreateDatabasePath();
        var rootDirectory = Path.GetDirectoryName(databasePath)!;
        var storage = new TestLocalStoragePathService(
            Path.Combine(rootDirectory, "Data"),
            Path.Combine(rootDirectory, "Backups"),
            Path.Combine(rootDirectory, "Pending"),
            Path.Combine(rootDirectory, "Logs"));
        databasePath = Path.Combine(storage.DataDirectory, "highcool.db");

        try
        {
            storage.EnsureRequiredDirectories();
            await CreateHighCoolSentinelDatabaseAsync(databasePath);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var service = CreateBackupService(databasePath, storage);

            var result = await service.CreateBackupAsync(BackupReason.Manual, cancellationTokenSource.Token);

            Assert.Equal(BackupStatus.Canceled, result.Status);
            Assert.Empty(Directory.EnumerateFiles(storage.BackupDirectory, "HighCool_*.db.enc"));
            Assert.Empty(Directory.EnumerateFiles(storage.PendingBackupDirectory));
        }
        finally
        {
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    private static AppDbContext CreateDbContext(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new AppDbContext(options);
    }

    private static DatabaseConfigurationService CreateDatabaseConfigurationService(
        string databasePath,
        ILocalStoragePathService? storage = null)
    {
        storage ??= new TestLocalStoragePathService(Path.GetDirectoryName(databasePath)!);
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:SqliteFileName"] = Path.GetFileName(databasePath)
        });

        return new DatabaseConfigurationService(configuration, storage);
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string CreateDatabasePath()
    {
        var directory = CreateTempDirectory();
        return Path.Combine(directory, $"{Guid.NewGuid():N}.db");
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static SqliteDatabaseBackupService CreateBackupService(
        string databasePath,
        ILocalStoragePathService storage)
    {
        var dbContext = CreateDbContext(databasePath);
        var metadataService = new ApplicationDatabaseMetadataService(dbContext);
        return new SqliteDatabaseBackupService(
            CreateDatabaseConfigurationService(databasePath, storage),
            storage,
            metadataService,
            new DevelopmentFileBackupEncryptionKeyProvider(storage),
            new BackupManifestService());
    }

    private static async Task CreateHighCoolSentinelDatabaseAsync(string databasePath)
    {
        await using var dbContext = CreateDbContext(databasePath);
        await dbContext.Database.EnsureCreatedAsync();

        var metadataService = new ApplicationDatabaseMetadataService(dbContext);
        await metadataService.EnsureInitializedAsync(CancellationToken.None);

        dbContext.Uoms.Add(new Uom
        {
            Code = "B2S",
            Name = "Batch 2 Sentinel",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "test"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task CreateSentinelDatabaseAsync(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE sentinel_records (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL
            );

            INSERT INTO sentinel_records (id, name)
            VALUES ('sentinel', 'backup-test');
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> SentinelExistsAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM uoms WHERE code = 'B2S';";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<string> RunIntegrityCheckAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static bool IsSameOrChildPath(string parentPath, string candidatePath)
    {
        var parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteIfExists(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        DeleteDirectoryIfExists(Path.GetDirectoryName(databasePath));
    }

    private static void DeleteSqliteFileSet(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void DeleteDirectoryIfExists(string? directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private sealed class TestLocalStoragePathService : ILocalStoragePathService
    {
        public TestLocalStoragePathService(string dataDirectory)
            : this(
                dataDirectory,
                Path.Combine(dataDirectory, "Backups"),
                Path.Combine(dataDirectory, "PendingBackups"),
                Path.Combine(dataDirectory, "Logs"))
        {
        }

        public TestLocalStoragePathService(
            string dataDirectory,
            string backupDirectory,
            string pendingBackupDirectory,
            string logDirectory)
        {
            DataDirectory = dataDirectory;
            BackupDirectory = backupDirectory;
            PendingBackupDirectory = pendingBackupDirectory;
            LogDirectory = logDirectory;
        }

        public string DataDirectory { get; }

        public string BackupDirectory { get; }

        public string PendingBackupDirectory { get; }

        public string LogDirectory { get; }

        public string GetSqliteDatabasePath(string fileName) => Path.Combine(DataDirectory, fileName);

        public void EnsureRequiredDirectories()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(PendingBackupDirectory);
            Directory.CreateDirectory(LogDirectory);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "ERP.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(Directory.GetCurrentDirectory());
    }
}
