using ERP.Application.LocalData;
using ERP.Domain.MasterData;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure;

public sealed class DevelopmentDatabaseInitializer(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    IDatabaseConfigurationService databaseConfigurationService,
    ILogger<DevelopmentDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var databaseConfiguration = databaseConfigurationService.GetConfiguration();

        if (!string.Equals(databaseConfiguration.Provider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var metadataService = scope.ServiceProvider.GetRequiredService<IApplicationDatabaseMetadataService>();

        await EnsureSqliteDatabaseIsReadyAsync(dbContext, metadataService, cancellationToken);

        if (hostEnvironment.IsDevelopment())
        {
            await SeedAsync(dbContext, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureSqliteDatabaseIsReadyAsync(
        AppDbContext dbContext,
        IApplicationDatabaseMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var databasePath = TryGetSqliteDatabasePath(dbContext);

        try
        {
            if (!string.IsNullOrWhiteSpace(databasePath) && File.Exists(databasePath))
            {
                var missingRequiredTables = await GetMissingRequiredSchemaTablesAsync(dbContext, cancellationToken);
                if (await HasApplicationTablesAsync(dbContext, cancellationToken) &&
                    missingRequiredTables.Count > 0)
                {
                    await HandleUnsupportedSchemaAsync(dbContext, databasePath, missingRequiredTables, cancellationToken);
                }
            }

            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            var metadata = await metadataService.EnsureInitializedAsync(cancellationToken);
            logger.LogInformation(
                "HighCool local database schema version {SchemaVersion} initialized for application version {ApplicationVersion}.",
                metadata.DatabaseSchemaVersion,
                metadata.ApplicationVersion);
        }
        catch (SqliteException exception) when (!string.IsNullOrWhiteSpace(databasePath) && File.Exists(databasePath))
        {
            if (CanResetDevelopmentDatabase())
            {
                await ResetSqliteDatabaseFileAsync(dbContext, databasePath, cancellationToken);
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                await metadataService.EnsureInitializedAsync(cancellationToken);
                return;
            }

            throw new InvalidOperationException(
                "The configured SQLite database could not be opened and was not modified. Check the local database file or set LocalDatabase:AllowDevelopmentReset=true in the Development environment to recreate it.",
                exception);
        }
    }

    private async Task HandleUnsupportedSchemaAsync(
        AppDbContext dbContext,
        string databasePath,
        IReadOnlyCollection<string> missingRequiredTables,
        CancellationToken cancellationToken)
    {
        if (CanResetDevelopmentDatabase())
        {
            await ResetSqliteDatabaseFileAsync(dbContext, databasePath, cancellationToken);
            return;
        }

        throw new InvalidOperationException(
            $"The configured SQLite database has an unsupported or incomplete HighCool schema and was not modified. Missing required tables: {string.Join(", ", missingRequiredTables)}. Set LocalDatabase:AllowDevelopmentReset=true only in the Development environment to recreate it.");
    }

    private bool CanResetDevelopmentDatabase()
        => hostEnvironment.IsDevelopment() &&
           bool.TryParse(configuration[$"{LocalDatabaseOptions.SectionName}:AllowDevelopmentReset"], out var allowReset) &&
           allowReset;

    private async Task ResetSqliteDatabaseFileAsync(
        AppDbContext dbContext,
        string databasePath,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "LocalDatabase:AllowDevelopmentReset is enabled in Development. HighCool will delete and recreate the configured SQLite development database.");
        cancellationToken.ThrowIfCancellationRequested();
        await dbContext.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    private static string? TryGetSqliteDatabasePath(AppDbContext dbContext)
    {
        var connectionString = dbContext.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
            {
                return null;
            }

            return Path.GetFullPath(builder.DataSource);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static async Task<bool> HasApplicationTablesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%'
                  AND name <> '__EFMigrationsHistory';
                """;

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyCollection<string>> GetMissingRequiredSchemaTablesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var requiredTables = new[]
        {
            "Organizations",
            "OrganizationSecuritySettings",
            "UserAccounts",
            "OrganizationMemberships",
            "Roles",
            "UserProfiles",
            "UserSessions",
            "EmailVerificationTokens",
            "AuditLogEntries"
        };

        var missingTables = new List<string>();
        foreach (var tableName in requiredTables)
        {
            if (!await TableExistsAsync(dbContext, tableName, cancellationToken))
            {
                missingTables.Add(tableName);
            }
        }

        return missingTables;
    }

    private static async Task<bool> TableExistsAsync(
        AppDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = $tableName;
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Uoms.AnyAsync(cancellationToken))
        {
            return;
        }

        var pieceUom = new Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "system"
        };

        var kilogramUom = new Uom
        {
            Code = "KG",
            Name = "Kilogram",
            Precision = 3,
            AllowsFraction = true,
            IsActive = true,
            CreatedBy = "system"
        };

        var mainWarehouse = new Warehouse
        {
            Code = "MAIN",
            Name = "Main Warehouse",
            Location = "Head Office",
            IsActive = true,
            CreatedBy = "system"
        };

        var outletWarehouse = new Warehouse
        {
            Code = "OUTLET",
            Name = "Outlet Warehouse",
            Location = "Retail Branch",
            IsActive = true,
            CreatedBy = "system"
        };

        var supplierA = new Supplier
        {
            Code = "SUP-001",
            Name = "Delta Cooling Supplies",
            StatementName = "Delta Cooling Supplies",
            Phone = "+20-100-000-0001",
            Email = "accounts@deltacooling.example",
            IsActive = true,
            CreatedBy = "system"
        };

        var supplierB = new Supplier
        {
            Code = "SUP-002",
            Name = "Nile Components Trading",
            StatementName = "Nile Components Trading",
            Phone = "+20-100-000-0002",
            Email = "sales@nilecomponents.example",
            IsActive = true,
            CreatedBy = "system"
        };

        var customerA = new Customer
        {
            Code = "CUS-001",
            Name = "Cairo Retail Projects",
            Phone = "+20-120-000-1001",
            Email = "ap@cairoretail.example",
            TaxNumber = "TAX-1001",
            Address = "Nasr City, Building 18",
            City = "Cairo",
            Area = "Nasr City",
            CreditLimit = 150000m,
            PaymentTerms = "30 days",
            Notes = "Priority retail account.",
            IsActive = true,
            CreatedBy = "system"
        };

        var customerB = new Customer
        {
            Code = "CUS-002",
            Name = "Alex Service Center",
            Phone = "+20-120-000-1002",
            Email = "finance@alexservice.example",
            TaxNumber = "TAX-1002",
            Address = "Smouha Industrial Zone",
            City = "Alexandria",
            Area = "Smouha",
            CreditLimit = 90000m,
            PaymentTerms = "Cash on delivery",
            Notes = "Requires delivery coordination before dispatch.",
            IsActive = true,
            CreatedBy = "system"
        };

        var fanMotor = new Item
        {
            Code = "ITM-001",
            Name = "Fan Motor",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "system"
        };

        var copperCoil = new Item
        {
            Code = "ITM-002",
            Name = "Copper Coil",
            BaseUomId = kilogramUom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "system"
        };

        var coolingUnit = new Item
        {
            Code = "ITM-003",
            Name = "Cooling Unit",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = "system"
        };

        var itemComponent = new ItemComponent
        {
            ItemId = coolingUnit.Id,
            ComponentItemId = fanMotor.Id,
            UomId = pieceUom.Id,
            Quantity = 1m,
            CreatedBy = "system"
        };

        var itemConversion = new UomConversion
        {
            FromUomId = pieceUom.Id,
            ToUomId = kilogramUom.Id,
            Factor = 0.25m,
            RoundingMode = RoundingMode.Round,
            IsActive = true,
            CreatedBy = "system"
        };

        var transitShortageReason = new ShortageReasonCode
        {
            Code = "TRANSIT_SHORTAGE",
            Name = "Transit shortage",
            Description = "Quantity was short during receipt capture and needs investigation.",
            AffectsSupplierBalance = false,
            AffectsStock = false,
            IsActive = true,
            CreatedBy = "system"
        };

        var supplierShortageReason = new ShortageReasonCode
        {
            Code = "SUPPLIER_SHORT",
            Name = "Supplier short supply",
            Description = "Supplier delivered less than expected and the shortage should affect supplier follow-up.",
            AffectsSupplierBalance = true,
            AffectsStock = false,
            IsActive = true,
            CreatedBy = "system"
        };

        dbContext.Uoms.AddRange(pieceUom, kilogramUom);
        dbContext.Warehouses.AddRange(mainWarehouse, outletWarehouse);
        dbContext.Customers.AddRange(customerA, customerB);
        dbContext.Suppliers.AddRange(supplierA, supplierB);
        dbContext.Items.AddRange(fanMotor, copperCoil, coolingUnit);
        dbContext.ItemComponents.Add(itemComponent);
        dbContext.UomConversions.Add(itemConversion);
        dbContext.ShortageReasonCodes.AddRange(transitShortageReason, supplierShortageReason);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
