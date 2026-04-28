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

public sealed class PurchaseReceiptDraftApiTests : IClassFixture<PurchaseReceiptDraftApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public PurchaseReceiptDraftApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PurchaseReceiptsApi_ShouldCreateListAndUpdateDrafts()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var supplier = new Domain.MasterData.Supplier
        {
            Code = "SUP-01",
            Name = "Supplier 01",
            StatementName = "Supplier 01",
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

        var item = new Domain.MasterData.Item
        {
            Code = "ITM-01",
            Name = "Item 01",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = "seed"
        };

        var component = new Domain.MasterData.Item
        {
            Code = "CMP-01",
            Name = "Component 01",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(item, component);
        await dbContext.SaveChangesAsync();

        dbContext.ItemComponents.Add(new Domain.MasterData.ItemComponent
        {
            ItemId = item.Id,
            ComponentItemId = component.Id,
            UomId = uom.Id,
            Quantity = 1m,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/purchase-receipts", new
        {
            receiptNo = "PRD-API-0001",
            supplierId = supplier.Id,
            warehouseId = warehouse.Id,
            receiptDate = DateTime.UtcNow.Date,
            notes = "API draft",
            lines = new[]
            {
                new
                {
                    lineNo = 1,
                    itemId = item.Id,
                    orderedQtySnapshot = 10m,
                    receivedQty = 9m,
                    uomId = uom.Id,
                    notes = "Line",
                    components = new[]
                    {
                        new
                        {
                            componentItemId = component.Id,
                            actualReceivedQty = 9m,
                            uomId = uom.Id,
                            notes = "Actual component"
                        }
                    }
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<PurchaseReceiptApiResponse>();
        Assert.NotNull(created);
        Assert.Equal("Draft", created!.Status);
        Assert.Single(created.Lines);

        var listResponse = await client.GetAsync("/api/purchase-receipts");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var drafts = await listResponse.Content.ReadFromJsonAsync<PaginatedResponse<PurchaseReceiptListApiResponse>>();
        Assert.NotNull(drafts);
        Assert.Single(drafts!.Items);

        var updateResponse = await client.PutAsJsonAsync($"/api/purchase-receipts/{created.Id}", new
        {
            receiptNo = "PRD-API-0001",
            supplierId = supplier.Id,
            warehouseId = warehouse.Id,
            receiptDate = DateTime.UtcNow.Date,
            notes = "Updated API draft",
            lines = new[]
            {
                new
                {
                    lineNo = 1,
                    itemId = item.Id,
                    orderedQtySnapshot = 12m,
                    receivedQty = 11m,
                    uomId = uom.Id,
                    notes = "Updated line",
                    components = new[]
                    {
                        new
                        {
                            componentItemId = component.Id,
                            actualReceivedQty = 11m,
                            uomId = uom.Id,
                            notes = "Updated component"
                        }
                    }
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<PurchaseReceiptApiResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated API draft", updated!.Notes);
        Assert.Equal(11m, updated.Lines[0].ReceivedQty);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-pr-api-tests-{Guid.NewGuid():N}.db");

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

    public sealed record PurchaseReceiptListApiResponse(Guid Id, string ReceiptNo, string Status, int LineCount);

    public sealed record PurchaseReceiptApiResponse(Guid Id, string ReceiptNo, string Notes, string Status, List<PurchaseReceiptLineApiResponse> Lines);

    public sealed record PurchaseReceiptLineApiResponse(int LineNo, decimal ReceivedQty, List<PurchaseReceiptLineComponentApiResponse> Components);

    public sealed record PurchaseReceiptLineComponentApiResponse(Guid ComponentItemId, decimal ActualReceivedQty);
}
