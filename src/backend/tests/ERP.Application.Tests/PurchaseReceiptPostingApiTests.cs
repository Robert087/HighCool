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

public sealed class PurchaseReceiptPostingApiTests : IClassFixture<PurchaseReceiptPostingApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public PurchaseReceiptPostingApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PurchaseReceiptsApi_ShouldPostDraftAndRemainIdempotent()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var supplier = new Domain.MasterData.Supplier
        {
            Code = "SUP-POST",
            Name = "Supplier",
            StatementName = "Supplier",
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

        var parentItem = new Domain.MasterData.Item
        {
            Code = "ITM-POST",
            Name = "Parent",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = "seed"
        };

        var componentItem = new Domain.MasterData.Item
        {
            Code = "CMP-POST",
            Name = "Component",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(parentItem, componentItem);
        await dbContext.SaveChangesAsync();

        dbContext.ItemComponents.Add(new Domain.MasterData.ItemComponent
        {
            ItemId = parentItem.Id,
            ComponentItemId = componentItem.Id,
            UomId = pieceUom.Id,
            Quantity = 2m,
            CreatedBy = "seed"
        });

        var shortageReason = new Domain.Shortages.ShortageReasonCode
        {
            Code = "SUPPLIER_SHORT",
            Name = "Supplier short supply",
            AffectsSupplierBalance = true,
            AffectsStock = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.ShortageReasonCodes.Add(shortageReason);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/purchase-receipts", new
        {
            receiptNo = "PRD-POST-0001",
            supplierId = supplier.Id,
            warehouseId = warehouse.Id,
            receiptDate = DateTime.UtcNow.Date,
            supplierPayableAmount = 500m,
            notes = "Ready to post",
            lines = new[]
            {
                new
                {
                    lineNo = 1,
                    itemId = parentItem.Id,
                    orderedQty = 2m,
                    receivedQty = 2m,
                    uomId = boxUom.Id,
                    notes = "Line",
                    components = new[]
                    {
                        new
                        {
                            componentItemId = componentItem.Id,
                            actualReceivedQty = 38m,
                            uomId = pieceUom.Id,
                            shortageReasonCodeId = shortageReason.Id,
                            notes = "Short"
                        }
                    }
                }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PurchaseReceiptApiResponse>();
        Assert.NotNull(created);

        var postResponse = await client.PostAsync($"/api/purchase-receipts/{created!.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var posted = await postResponse.Content.ReadFromJsonAsync<PurchaseReceiptApiResponse>();
        Assert.NotNull(posted);
        Assert.Equal("Posted", posted!.Status);

        var secondPostResponse = await client.PostAsync($"/api/purchase-receipts/{created.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, secondPostResponse.StatusCode);

        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync());
        Assert.Equal(1, await dbContext.ShortageLedgerEntries.CountAsync());
        Assert.Equal(1, await dbContext.SupplierStatementEntries.CountAsync());
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-pr-post-api-tests-{Guid.NewGuid():N}.db");

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
                AuthenticatedApiTestSupport.ConfigureServices(services);
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
            await AuthenticatedApiTestSupport.SeedAuthenticatedContextAsync(scope.ServiceProvider, dbContext);
        }
    }

    public sealed record PurchaseReceiptApiResponse(Guid Id, string ReceiptNo, string Status);
}
