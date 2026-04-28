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

public sealed class StockLedgerApiTests : IClassFixture<StockLedgerApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public StockLedgerApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StockLedgerApis_ShouldReturnMovementsAndBalances()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pieceUom = new Domain.MasterData.Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var supplier = new Domain.MasterData.Supplier
        {
            Code = "SUP-LEDGER",
            Name = "Ledger Supplier",
            StatementName = "Ledger Supplier",
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

        dbContext.Uoms.Add(pieceUom);
        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Domain.MasterData.Item
        {
            Code = "ITM-LEDGER",
            Name = "Ledger Item",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        var receipt = new Domain.Purchasing.PurchaseReceipt
        {
            ReceiptNo = "PR-LEDGER-0001",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            ReceiptDate = new DateTime(2026, 4, 20),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        dbContext.StockLedgerEntries.Add(new Domain.Inventory.StockLedgerEntry
        {
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            TransactionType = Domain.Inventory.StockTransactionType.PurchaseReceipt,
            SourceDocType = Domain.Inventory.SourceDocumentType.PurchaseReceipt,
            SourceDocId = receipt.Id,
            SourceLineId = Guid.NewGuid(),
            QtyIn = 5m,
            QtyOut = 0m,
            UomId = pieceUom.Id,
            BaseQty = 5m,
            RunningBalanceQty = 5m,
            TransactionDate = receipt.ReceiptDate,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var movementResponse = await client.GetAsync($"/api/stock-ledger/item/{item.Id}?warehouseId={warehouse.Id}");
        Assert.Equal(HttpStatusCode.OK, movementResponse.StatusCode);
        var movements = await movementResponse.Content.ReadFromJsonAsync<PaginatedResponse<StockLedgerEntryResponse>>();
        var movement = Assert.Single(movements!.Items);
        Assert.Equal("PR-LEDGER-0001", movement.SourceDocumentNo);

        var balanceResponse = await client.GetAsync($"/api/stock-balance/item/{item.Id}?warehouseId={warehouse.Id}");
        Assert.Equal(HttpStatusCode.OK, balanceResponse.StatusCode);
        var balances = await balanceResponse.Content.ReadFromJsonAsync<PaginatedResponse<StockBalanceResponse>>();
        var balance = Assert.Single(balances!.Items);
        Assert.Equal(5m, balance.BalanceQty);
    }

    [Fact]
    public async Task StockLedgerApis_ShouldRejectInvalidDateRange()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/stock-ledger?fromDate=2026-04-21&toDate=2026-04-20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StockLedgerApis_ShouldReturnPostedReceiptEffects()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var supplier = new Domain.MasterData.Supplier
        {
            Code = "SUP-STOCK-POST",
            Name = "Stock Posted Supplier",
            StatementName = "Stock Posted Supplier",
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

        var pieceUom = new Domain.MasterData.Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var boxUom = new Domain.MasterData.Uom
        {
            Code = "BOX",
            Name = "Box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Uoms.AddRange(pieceUom, boxUom);
        await dbContext.SaveChangesAsync();

        dbContext.UomConversions.Add(new Domain.MasterData.UomConversion
        {
            FromUomId = boxUom.Id,
            ToUomId = pieceUom.Id,
            Factor = 10m,
            RoundingMode = Domain.MasterData.RoundingMode.None,
            IsActive = true,
            CreatedBy = "seed"
        });

        var item = new Domain.MasterData.Item
        {
            Code = "ITM-STOCK-POST",
            Name = "Posted Receipt Item",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/purchase-receipts", new
        {
            receiptNo = "PR-STOCK-0001",
            supplierId = supplier.Id,
            warehouseId = warehouse.Id,
            receiptDate = new DateTime(2026, 4, 20),
            notes = "Stock query test",
            lines = new[]
            {
                new
                {
                    lineNo = 1,
                    itemId = item.Id,
                    orderedQtySnapshot = 2m,
                    receivedQty = 2m,
                    uomId = boxUom.Id,
                    notes = "Posted stock line",
                    components = Array.Empty<object>()
                }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PurchaseReceiptApiResponse>();
        Assert.NotNull(created);

        var postResponse = await client.PostAsync($"/api/purchase-receipts/{created!.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var movementResponse = await client.GetAsync($"/api/stock-ledger?itemId={item.Id}&warehouseId={warehouse.Id}");
        Assert.Equal(HttpStatusCode.OK, movementResponse.StatusCode);
        var movements = await movementResponse.Content.ReadFromJsonAsync<PaginatedResponse<StockLedgerMovementResponse>>();
        var movement = Assert.Single(movements!.Items);
        Assert.Equal("PR-STOCK-0001", movement.SourceDocumentNo);
        Assert.Equal(20m, movement.RunningBalanceQty);

        var balanceResponse = await client.GetAsync($"/api/stock-balance?itemId={item.Id}&warehouseId={warehouse.Id}");
        Assert.Equal(HttpStatusCode.OK, balanceResponse.StatusCode);
        var balances = await balanceResponse.Content.ReadFromJsonAsync<PaginatedResponse<StockBalanceResponse>>();
        var balance = Assert.Single(balances!.Items);
        Assert.Equal(20m, balance.BalanceQty);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-stock-api-tests-{Guid.NewGuid():N}.db");

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

    public sealed record StockLedgerEntryResponse(Guid Id, string SourceDocumentNo);

    public sealed record StockLedgerMovementResponse(Guid Id, string SourceDocumentNo, decimal RunningBalanceQty);

    public sealed record StockBalanceResponse(Guid ItemId, Guid WarehouseId, decimal BalanceQty);

    public sealed record PurchaseReceiptApiResponse(Guid Id, string ReceiptNo, string Status);
}
