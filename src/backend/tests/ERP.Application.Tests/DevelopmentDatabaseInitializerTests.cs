using ERP.Application.LocalData;
using ERP.Domain.MasterData;
using ERP.Infrastructure;
using ERP.Infrastructure.LocalData;
using ERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class DevelopmentDatabaseInitializerTests
{
    [Fact]
    public async Task StartAsync_SeedsNewSqliteDatabase_AndCreatesMetadata()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await using var provider = CreateProvider(databasePath);
            var initializer = CreateInitializer(provider, databasePath);

            await initializer.StartAsync(CancellationToken.None);

            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.True(await dbContext.Customers.AnyAsync());
            Assert.True(await dbContext.Suppliers.AnyAsync());
            Assert.True(await dbContext.Warehouses.AnyAsync());
            Assert.True(await dbContext.Uoms.AnyAsync());
            Assert.True(await dbContext.Items.AnyAsync());
            Assert.True(await dbContext.ItemComponents.AnyAsync());
            Assert.True(await dbContext.UomConversions.AnyAsync());
            Assert.True(await dbContext.ApplicationDatabaseMetadata.IgnoreQueryFilters().AnyAsync());
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task StartAsync_RunTwice_PreservesSentinelRecordAndDatabaseFile()
    {
        var databasePath = CreateDatabasePath();
        var sentinelCode = $"SENT-{Guid.NewGuid():N}"[..16];

        try
        {
            await using var provider = CreateProvider(databasePath);
            var initializer = CreateInitializer(provider, databasePath);

            await initializer.StartAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Uoms.Add(new Uom
                {
                    Code = sentinelCode,
                    Name = "Sentinel UOM",
                    Precision = 0,
                    AllowsFraction = false,
                    IsActive = true,
                    CreatedBy = "test"
                });

                await dbContext.SaveChangesAsync();
            }

            var createdAt = File.GetCreationTimeUtc(databasePath);

            await initializer.StartAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.True(await dbContext.Uoms.IgnoreQueryFilters().AnyAsync(entity => entity.Code == sentinelCode));
            }

            Assert.True(File.Exists(databasePath));
            Assert.Equal(createdAt, File.GetCreationTimeUtc(databasePath));
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task StartAsync_UnsupportedSchema_DoesNotResetWhenOptionIsFalseOrMissing(bool? allowReset)
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await CreatePartialDatabaseAsync(databasePath);

            await using var provider = CreateProvider(databasePath, allowReset);
            var initializer = CreateInitializer(provider, databasePath, allowReset);

            await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.StartAsync(CancellationToken.None));

            Assert.True(File.Exists(databasePath));
            Assert.True(await TableExistsAsync(databasePath, "suppliers"));
            Assert.False(await TableExistsAsync(databasePath, "customers"));
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task StartAsync_MalformedSqliteFile_FailsWithoutDeleting()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await File.WriteAllTextAsync(databasePath, "not a sqlite database");

            await using var provider = CreateProvider(databasePath);
            var initializer = CreateInitializer(provider, databasePath);

            await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.StartAsync(CancellationToken.None));

            Assert.True(File.Exists(databasePath));
            Assert.Equal("not a sqlite database", await File.ReadAllTextAsync(databasePath));
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task StartAsync_ExplicitReset_RebuildsUnsupportedSchemaOnlyInDevelopment()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await CreatePartialDatabaseAsync(databasePath);

            await using var provider = CreateProvider(databasePath, allowReset: true);
            var initializer = CreateInitializer(provider, databasePath, allowReset: true);

            await initializer.StartAsync(CancellationToken.None);

            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.True(await dbContext.Customers.AnyAsync());
            Assert.True(await dbContext.ApplicationDatabaseMetadata.IgnoreQueryFilters().AnyAsync());
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task StartAsync_ExplicitReset_IsIgnoredOutsideDevelopment()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await CreatePartialDatabaseAsync(databasePath);

            await using var provider = CreateProvider(databasePath, allowReset: true);
            var initializer = CreateInitializer(
                provider,
                databasePath,
                allowReset: true,
                environmentName: Environments.Production);

            await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.StartAsync(CancellationToken.None));

            Assert.True(await TableExistsAsync(databasePath, "suppliers"));
            Assert.False(await TableExistsAsync(databasePath, "customers"));
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    [Fact]
    public async Task StartAsync_LegacyDesktopFoundationDatabaseWithoutEfHistory_AppliesPendingMigrations()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var installationId = await CreateLegacyDesktopFoundationDatabaseWithoutMigrationHistoryAsync(databasePath);

            await using var provider = CreateProvider(databasePath);
            var initializer = CreateInitializer(provider, databasePath, environmentName: Environments.Production);

            await initializer.StartAsync(CancellationToken.None);

            Assert.True(await TableExistsAsync(databasePath, "__EFMigrationsHistory"));
            Assert.True(await TableExistsAsync(databasePath, "item_categories"));
            Assert.True(await ColumnExistsAsync(databasePath, "Organizations", "EnableEmployeeAdvances"));
            Assert.True(await ColumnExistsAsync(databasePath, "items", "minimum_stock_quantity"));
            Assert.Equal(installationId, await GetInstallationIdAsync(databasePath));
            Assert.True(await MigrationHistoryContainsAsync(databasePath, "20260727225601_AddOrganizationFeatureGatesPhase2"));
            Assert.True(await MigrationHistoryContainsAsync(databasePath, "20260727235401_Phase3InventoryFoundation"));
        }
        finally
        {
            DeleteIfExists(databasePath);
        }
    }

    private static ServiceProvider CreateProvider(string databasePath, bool? allowReset = false)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(SqliteTestDatabase.CreateConnectionString(databasePath)));
        services.AddScoped<IApplicationDatabaseMetadataService, ApplicationDatabaseMetadataService>();
        return services.BuildServiceProvider();
    }

    private static DevelopmentDatabaseInitializer CreateInitializer(
        IServiceProvider provider,
        string databasePath,
        bool? allowReset = false,
        string environmentName = "Development")
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:SqliteFileName"] = Path.GetFileName(databasePath)
        };

        if (allowReset.HasValue)
        {
            configurationValues["LocalDatabase:AllowDevelopmentReset"] = allowReset.Value.ToString();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var storage = new TestLocalStoragePathService(Path.GetDirectoryName(databasePath)!);
        var databaseConfigurationService = new DatabaseConfigurationService(configuration, storage);

        return new DevelopmentDatabaseInitializer(
            provider,
            configuration,
            new TestHostEnvironment
            {
                EnvironmentName = environmentName,
                ContentRootPath = Path.GetDirectoryName(databasePath)!
            },
            databaseConfigurationService,
            NullLogger<DevelopmentDatabaseInitializer>.Instance);
    }

    private static string CreateDatabasePath()
        => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string databasePath)
    {
        SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
    }

    private static async Task CreatePartialDatabaseAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(databasePath));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE "suppliers" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_suppliers" PRIMARY KEY,
                "code" TEXT NOT NULL,
                "name" TEXT NOT NULL,
                "statement_name" TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> CreateLegacyDesktopFoundationDatabaseWithoutMigrationHistoryAsync(string databasePath)
    {
        await using (var provider = CreateProvider(databasePath))
        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var migrator = dbContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260725124500_AddDesktopFoundationBatch2Safety");

            var metadataService = scope.ServiceProvider.GetRequiredService<IApplicationDatabaseMetadataService>();
            var metadata = await metadataService.EnsureInitializedAsync(CancellationToken.None);

            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE "__EFMigrationsHistory";""");
            return metadata.InstallationId;
        }
    }

    private static async Task<bool> TableExistsAsync(string databasePath, string tableName)
    {
        await using var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(databasePath));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $tableName;
            """;

        command.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(string databasePath, string tableName, string columnName)
    {
        await using var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(databasePath));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"", StringComparison.Ordinal)}\");";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> MigrationHistoryContainsAsync(string databasePath, string migrationId)
    {
        await using var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(databasePath));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = $migrationId;
            """;
        command.Parameters.AddWithValue("$migrationId", migrationId);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<string> GetInstallationIdAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(databasePath));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "installation_id"
            FROM "application_database_metadata"
            LIMIT 1;
            """;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private sealed class TestLocalStoragePathService(string dataDirectory) : ILocalStoragePathService
    {
        public string DataDirectory { get; } = dataDirectory;

        public string BackupDirectory { get; } = Path.Combine(dataDirectory, "Backups");

        public string PendingBackupDirectory { get; } = Path.Combine(dataDirectory, "PendingBackups");

        public string LogDirectory { get; } = Path.Combine(dataDirectory, "Logs");

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
