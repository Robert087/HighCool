using ERP.Application.MasterData.Items;
using ERP.Application.MasterData.Customers;
using ERP.Application.MasterData.Suppliers;
using ERP.Application.MasterData.UomConversions;
using ERP.Application.MasterData.Uoms;
using ERP.Application.MasterData.Warehouses;
using ERP.Application.Inventory;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Application.Purchasing.ShortageReasonCodes;
using ERP.Application.Shortages;
using ERP.Infrastructure.Inventory;
using ERP.Infrastructure.MasterData.Items;
using ERP.Infrastructure.MasterData.Customers;
using ERP.Infrastructure.MasterData.Suppliers;
using ERP.Infrastructure.MasterData.UomConversions;
using ERP.Infrastructure.MasterData.Uoms;
using ERP.Infrastructure.MasterData.Warehouses;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using ERP.Infrastructure.Purchasing.PurchaseOrders;
using ERP.Infrastructure.Purchasing.ShortageReasonCodes;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Shortages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                EnsureSqliteDirectoryExists(connectionString);
                options.UseSqlite(connectionString);
                return;
            }

            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        });

        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IUomConversionService, UomConversionService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IUomService, UomService>();
        services.AddScoped<IStockLedgerQueryService, StockLedgerQueryService>();
        services.AddScoped<IStockBalanceService, StockBalanceService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IPurchaseOrderPostingService, PurchaseOrderPostingService>();
        services.AddScoped<IPurchaseOrderCancellationService, PurchaseOrderCancellationService>();
        services.AddScoped<IPurchaseReceiptService, PurchaseReceiptService>();
        services.AddScoped<IPurchaseReceiptPostingService, PurchaseReceiptPostingService>();
        services.AddScoped<IQuantityConversionService, QuantityConversionService>();
        services.AddScoped<IStockLedgerService, StockLedgerService>();
        services.AddScoped<IShortageDetectionService, ShortageDetectionService>();
        services.AddScoped<IShortageReasonCodeService, ShortageReasonCodeService>();
        services.AddScoped<IShortageResolutionService, ShortageResolutionService>();
        services.AddScoped<IShortageResolutionPostingService, ShortageResolutionPostingService>();
        services.AddScoped<IShortageResolutionValidationService, ShortageResolutionValidationService>();
        services.AddScoped<IShortageResolutionAllocationService, ShortageResolutionAllocationService>();
        services.AddHostedService<DevelopmentDatabaseInitializer>();

        return services;
    }

    private static void EnsureSqliteDirectoryExists(string connectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
            {
                return;
            }

            var databasePath = Path.GetFullPath(builder.DataSource);
            var directory = Path.GetDirectoryName(databasePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The configured SQLite connection string is invalid.",
                exception);
        }
    }
}
