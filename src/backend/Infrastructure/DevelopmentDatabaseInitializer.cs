using ERP.Domain.MasterData;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ERP.Infrastructure;

public sealed class DevelopmentDatabaseInitializer(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var provider = configuration["DatabaseProvider"] ?? "SqlServer";

        if (!hostEnvironment.IsDevelopment() ||
            !string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await EnsureSqliteDatabaseIsReadyAsync(dbContext, cancellationToken);
        await SeedAsync(dbContext, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureSqliteDatabaseIsReadyAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var databasePath = TryGetSqliteDatabasePath(dbContext);

        if (!string.IsNullOrWhiteSpace(databasePath) &&
            File.Exists(databasePath) &&
            await IsPartiallyInitializedAsync(dbContext, cancellationToken))
        {
            await ResetSqliteDatabaseFileAsync(dbContext, databasePath);
        }

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (SqliteException) when (!string.IsNullOrWhiteSpace(databasePath) && File.Exists(databasePath))
        {
            await ResetSqliteDatabaseFileAsync(dbContext, databasePath);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }

    private static async Task ResetSqliteDatabaseFileAsync(AppDbContext dbContext, string databasePath)
    {
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

    private static async Task<bool> IsPartiallyInitializedAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name <> '__EFMigrationsHistory';
            """;

        var userTableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        if (userTableCount == 0)
        {
            return false;
        }

        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = '__EFMigrationsHistory';
            """;

        var hasMigrationHistoryTable = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;

        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }

        return !hasMigrationHistoryTable;
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
