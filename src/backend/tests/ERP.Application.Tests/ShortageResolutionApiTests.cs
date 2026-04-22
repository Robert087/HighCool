using System.Net;
using System.Net.Http.Json;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class ShortageResolutionApiTests : IClassFixture<ShortageResolutionApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ShortageResolutionApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShortageResolutionApis_ShouldCreatePostAndExposeQueries()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var supplier = new Domain.MasterData.Supplier
        {
            Code = "SUP-SR-API",
            Name = "Resolution Supplier",
            StatementName = "Resolution Supplier",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouse = new Domain.MasterData.Warehouse
        {
            Code = "MAIN",
            Name = "Main Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        var uom = new Domain.MasterData.Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Uoms.Add(uom);
        await dbContext.SaveChangesAsync();

        var parentItem = new Domain.MasterData.Item
        {
            Code = "ITM-SR-API",
            Name = "Resolution Parent",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = "seed"
        };

        var componentItem = new Domain.MasterData.Item
        {
            Code = "CMP-SR-API",
            Name = "Resolution Component",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(parentItem, componentItem);
        await dbContext.SaveChangesAsync();

        var receipt = new Domain.Purchasing.PurchaseReceipt
        {
            ReceiptNo = "PR-SR-API-0001",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            ReceiptDate = new DateTime(2026, 4, 21),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var receiptLine = new Domain.Purchasing.PurchaseReceiptLine
        {
            PurchaseReceiptId = receipt.Id,
            LineNo = 1,
            ItemId = parentItem.Id,
            ReceivedQty = 1m,
            UomId = uom.Id,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceiptLines.Add(receiptLine);
        await dbContext.SaveChangesAsync();

        var shortage = new Domain.Shortages.ShortageLedgerEntry
        {
            PurchaseReceiptId = receipt.Id,
            PurchaseReceiptLineId = receiptLine.Id,
            ItemId = parentItem.Id,
            ComponentItemId = componentItem.Id,
            ExpectedQty = 5m,
            ActualQty = 0m,
            ShortageQty = 5m,
            ResolvedPhysicalQty = 0m,
            ResolvedFinancialQtyEquivalent = 0m,
            OpenQty = 5m,
            ShortageValue = null,
            ResolvedAmount = 0m,
            OpenAmount = null,
            AffectsSupplierBalance = false,
            ApprovalStatus = "NotRequired",
            Status = Domain.Shortages.ShortageEntryStatus.Open,
            CreatedBy = "seed"
        };

        dbContext.ShortageLedgerEntries.Add(shortage);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/shortage-resolutions", new
        {
            resolutionNo = "SR-API-0001",
            supplierId = supplier.Id,
            resolutionType = "Financial",
            resolutionDate = new DateTime(2026, 4, 21),
            currency = "EGP",
            notes = "API resolution",
            allocations = new[]
            {
                new
                {
                    shortageLedgerId = shortage.Id,
                    allocatedQty = 2m,
                    valuationRate = 10m,
                    allocationMethod = "Manual",
                    sequenceNo = 1
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ShortageResolutionResponse>();
        Assert.NotNull(created);
        Assert.Equal("Draft", created!.Status);

        var postResponse = await client.PostAsync($"/api/shortage-resolutions/{created.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var posted = await postResponse.Content.ReadFromJsonAsync<ShortageResolutionResponse>();
        Assert.NotNull(posted);
        Assert.Equal("Posted", posted!.Status);

        var openShortagesResponse = await client.GetAsync($"/api/shortages/open?supplierId={supplier.Id}");
        Assert.Equal(HttpStatusCode.OK, openShortagesResponse.StatusCode);
        var openShortages = await openShortagesResponse.Content.ReadFromJsonAsync<OpenShortageResponse[]>();
        var openShortage = Assert.Single(openShortages!);
        Assert.Equal(3m, openShortage.OpenQty);
        Assert.Equal(30m, openShortage.OpenAmount);

        var allocationsResponse = await client.GetAsync($"/api/shortage-resolutions/{created.Id}/allocations");
        Assert.Equal(HttpStatusCode.OK, allocationsResponse.StatusCode);
        var allocations = await allocationsResponse.Content.ReadFromJsonAsync<ShortageResolutionAllocationResponse[]>();
        var allocation = Assert.Single(allocations!);
        Assert.Equal(20m, allocation.AllocatedAmount);

        Assert.Equal(1, await dbContext.SupplierStatementEntries.CountAsync());
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-shortage-resolution-api-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            });
        }

        public async Task InitializeAsync()
        {
            await ResetDatabaseAsync();
        }

        public new async Task DisposeAsync()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }

            await base.DisposeAsync();
        }

        public async Task ResetDatabaseAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }
    }

    private sealed record ShortageResolutionResponse(Guid Id, string ResolutionNo, string Status);

    private sealed record ShortageResolutionAllocationResponse(Guid Id, decimal? AllocatedAmount);

    private sealed record OpenShortageResponse(Guid Id, decimal OpenQty, decimal? OpenAmount);
}
