using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
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

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await SeedAsync(dbContext, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

        var fanMotor = new Item
        {
            Code = "ITM-001",
            Name = "Fan Motor",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            IsComponent = true,
            CreatedBy = "system"
        };

        var copperCoil = new Item
        {
            Code = "ITM-002",
            Name = "Copper Coil",
            BaseUomId = kilogramUom.Id,
            IsActive = true,
            IsSellable = false,
            IsComponent = true,
            CreatedBy = "system"
        };

        var coolingUnit = new Item
        {
            Code = "ITM-003",
            Name = "Cooling Unit",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            IsComponent = false,
            CreatedBy = "system"
        };

        var itemComponent = new ItemComponent
        {
            ParentItemId = coolingUnit.Id,
            ComponentItemId = fanMotor.Id,
            Quantity = 1m,
            CreatedBy = "system"
        };

        var itemConversion = new ItemUomConversion
        {
            ItemId = copperCoil.Id,
            FromUomId = kilogramUom.Id,
            ToUomId = kilogramUom.Id,
            Factor = 1m,
            RoundingMode = RoundingMode.None,
            MinFraction = 0m,
            IsActive = true,
            CreatedBy = "system"
        };

        dbContext.Uoms.AddRange(pieceUom, kilogramUom);
        dbContext.Warehouses.AddRange(mainWarehouse, outletWarehouse);
        dbContext.Suppliers.AddRange(supplierA, supplierB);
        dbContext.Items.AddRange(fanMotor, copperCoil, coolingUnit);
        dbContext.ItemComponents.Add(itemComponent);
        dbContext.ItemUomConversions.Add(itemConversion);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
