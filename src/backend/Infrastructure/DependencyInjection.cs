using ERP.Application.MasterData.ItemComponents;
using ERP.Application.MasterData.Items;
using ERP.Application.MasterData.ItemUomConversions;
using ERP.Application.MasterData.Suppliers;
using ERP.Application.MasterData.Uoms;
using ERP.Application.MasterData.Warehouses;
using ERP.Infrastructure.MasterData.ItemComponents;
using ERP.Infrastructure.MasterData.Items;
using ERP.Infrastructure.MasterData.ItemUomConversions;
using ERP.Infrastructure.MasterData.Suppliers;
using ERP.Infrastructure.MasterData.Uoms;
using ERP.Infrastructure.MasterData.Warehouses;
using ERP.Infrastructure.Persistence;
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
        var provider = configuration["DatabaseProvider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
                return;
            }

            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        });

        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IItemComponentService, ItemComponentService>();
        services.AddScoped<IItemUomConversionService, ItemUomConversionService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IUomService, UomService>();
        services.AddHostedService<DevelopmentDatabaseInitializer>();

        return services;
    }
}
