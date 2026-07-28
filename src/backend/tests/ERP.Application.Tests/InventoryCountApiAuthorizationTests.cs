using System.Net;
using System.Net.Http.Json;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class InventoryCountApiAuthorizationTests : IClassFixture<InventoryCountApiAuthorizationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public InventoryCountApiAuthorizationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InventoryCountsApi_ShouldEnforceFeatureGates()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPermissionsAsync(dbContext, Permissions.InventoryCountView);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        var client = _factory.CreateClient();

        organization.EnableInventory = false;
        organization.EnableInventoryCounts = true;
        await dbContext.SaveChangesAsync();
        var inventoryDisabled = await client.GetAsync("/api/inventory-counts");
        Assert.Equal(HttpStatusCode.Forbidden, inventoryDisabled.StatusCode);

        organization.EnableInventory = true;
        organization.EnableInventoryCounts = false;
        await dbContext.SaveChangesAsync();
        var countsDisabled = await client.GetAsync("/api/inventory-counts");
        Assert.Equal(HttpStatusCode.Forbidden, countsDisabled.StatusCode);

        organization.EnableInventoryCounts = true;
        await dbContext.SaveChangesAsync();
        var enabled = await client.GetAsync("/api/inventory-counts");
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
    }

    [Fact]
    public async Task InventoryCountsApi_ShouldEnforceViewCreateAndPostPermissions()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnableCountsAsync(dbContext);
        var references = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        await SetPermissionsAsync(dbContext);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory-counts")).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryCountView);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory-counts")).StatusCode);

        var viewOnlyCount = await SeedCountAsync(dbContext, references, DocumentStatus.Draft);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-counts/{viewOnlyCount.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryCountCreate);
        var createResponse = await client.PostAsJsonAsync("/api/inventory-counts", CreateRequest(references));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InventoryCountResponse>();
        Assert.NotNull(created);
        Assert.Equal("CNT-000001", created!.CountNo);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-counts/{created.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryCountPost);
        var postableCount = await SeedCountAsync(dbContext, references, DocumentStatus.Draft);
        await SeedBalanceAsync(dbContext, references, 5m);
        var postResponse = await client.PostAsync($"/api/inventory-counts/{postableCount.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var cancelResponse = await client.PostAsync($"/api/inventory-counts/{postableCount.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task InventoryCountsApi_ShouldHideCrossOrganizationDocuments()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnableCountsAsync(dbContext);
        await SetPermissionsAsync(dbContext, Permissions.InventoryCountView, Permissions.InventoryCountCreate, Permissions.InventoryCountPost);
        var otherCount = await SeedOtherOrganizationCountAsync(dbContext);
        var ownReferences = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        var listResponse = await client.GetFromJsonAsync<CountListResponse>("/api/inventory-counts?search=CNT-OTHER");
        Assert.NotNull(listResponse);
        Assert.Empty(listResponse!.Items);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inventory-counts/{otherCount}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/inventory-counts/{otherCount}", CreateRequest(ownReferences))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/inventory-counts/{otherCount}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-counts/{otherCount}/refresh-system-quantities", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-counts/{otherCount}/post", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-counts/{otherCount}/cancel", null)).StatusCode);
    }

    private static async Task EnableCountsAsync(AppDbContext dbContext)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        organization.EnableInventory = true;
        organization.EnableInventoryCounts = true;
        await dbContext.SaveChangesAsync();
    }

    private static async Task SetPermissionsAsync(AppDbContext dbContext, params string[] permissions)
    {
        var membership = await dbContext.OrganizationMemberships.IgnoreQueryFilters().SingleAsync();
        membership.IsOwner = false;

        dbContext.MembershipRoles.RemoveRange(dbContext.MembershipRoles.IgnoreQueryFilters());
        dbContext.RolePermissions.RemoveRange(dbContext.RolePermissions.IgnoreQueryFilters());
        dbContext.Roles.RemoveRange(dbContext.Roles.IgnoreQueryFilters());
        await dbContext.SaveChangesAsync();

        if (permissions.Length == 0)
        {
            return;
        }

        var role = new Role
        {
            OrganizationId = membership.OrganizationId,
            Name = $"Test role {Guid.NewGuid():N}",
            IsActive = true,
            CreatedBy = "test"
        };
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        dbContext.RolePermissions.AddRange(permissions.Distinct(StringComparer.OrdinalIgnoreCase).Select(permission => new RolePermission
        {
            OrganizationId = membership.OrganizationId,
            RoleId = role.Id,
            PermissionKey = permission,
            CreatedBy = "test"
        }));
        dbContext.MembershipRoles.Add(new MembershipRole
        {
            OrganizationId = membership.OrganizationId,
            MembershipId = membership.Id,
            RoleId = role.Id,
            CreatedBy = "test"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<CountReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var existing = await dbContext.Items.FirstOrDefaultAsync(entity => entity.Code == "ITM-CNT-API");
        if (existing is not null)
        {
            return new CountReferences(
                await dbContext.Warehouses.SingleAsync(entity => entity.Code == "WH-CNT-API"),
                await dbContext.Uoms.SingleAsync(entity => entity.Code == "PCS-CNT-API"),
                existing);
        }

        var uom = new Uom
        {
            Code = "PCS-CNT-API",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };
        var warehouse = new Warehouse
        {
            Code = "WH-CNT-API",
            Name = "Count API Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITM-CNT-API",
            Name = "Count API Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return new CountReferences(warehouse, uom, item);
    }

    private static async Task<InventoryCount> SeedCountAsync(
        AppDbContext dbContext,
        CountReferences references,
        DocumentStatus status)
    {
        var count = new InventoryCount
        {
            CountNo = $"CNT-API-{Guid.NewGuid():N}"[..24],
            CountDate = DateTime.UtcNow.Date,
            WarehouseId = references.Warehouse.Id,
            Notes = "API authorization test",
            Status = status,
            CreatedBy = "seed"
        };
        count.Lines.Add(new InventoryCountLine
        {
            LineNo = 1,
            ItemId = references.Item.Id,
            UomId = references.Uom.Id,
            SystemQty = 0m,
            CountedQty = 1m,
            VarianceQty = 1m,
            BaseSystemQty = 0m,
            BaseCountedQty = 1m,
            BaseVarianceQty = 1m,
            CreatedBy = "seed"
        });
        dbContext.InventoryCounts.Add(count);
        await dbContext.SaveChangesAsync();
        return count;
    }

    private static async Task SeedBalanceAsync(AppDbContext dbContext, CountReferences references, decimal baseQty)
    {
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = references.Warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = baseQty,
            QtyOut = 0m,
            UomId = references.Uom.Id,
            BaseQty = baseQty,
            RunningBalanceQty = baseQty,
            TransactionDate = DateTime.UtcNow.Date.AddDays(-1),
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedOtherOrganizationCountAsync(AppDbContext dbContext)
    {
        var organization = new Organization
        {
            Name = $"Other Org {Guid.NewGuid():N}",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            SetupCompleted = true,
            EnableInventory = true,
            EnableInventoryCounts = true,
            CreatedBy = "seed"
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var uom = new Uom { OrganizationId = organization.Id, Code = "PCS-CNT-OTHER", Name = "Pieces", IsActive = true, CreatedBy = "seed" };
        var warehouse = new Warehouse { OrganizationId = organization.Id, Code = "WH-CNT-OTHER", Name = "Other Count Warehouse", IsActive = true, CreatedBy = "seed" };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item { OrganizationId = organization.Id, Code = "ITM-CNT-OTHER", Name = "Other Item", BaseUomId = uom.Id, IsActive = true, IsSellable = true, CreatedBy = "seed" };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        var count = new InventoryCount
        {
            OrganizationId = organization.Id,
            CountNo = "CNT-OTHER-0001",
            CountDate = DateTime.UtcNow.Date,
            WarehouseId = warehouse.Id,
            Notes = "Other organization",
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };
        count.Lines.Add(new InventoryCountLine
        {
            OrganizationId = organization.Id,
            LineNo = 1,
            ItemId = item.Id,
            UomId = uom.Id,
            SystemQty = 0m,
            CountedQty = 1m,
            VarianceQty = 1m,
            BaseSystemQty = 0m,
            BaseCountedQty = 1m,
            BaseVarianceQty = 1m,
            CreatedBy = "seed"
        });
        dbContext.InventoryCounts.Add(count);
        await dbContext.SaveChangesAsync();
        return count.Id;
    }

    private static object CreateRequest(CountReferences references)
        => new
        {
            countNo = "CLIENT-CONTROLLED",
            countDate = DateTime.UtcNow.Date,
            warehouseId = references.Warehouse.Id,
            notes = "API test count",
            lines = new[]
            {
                new
                {
                    lineNo = 1,
                    itemId = references.Item.Id,
                    uomId = references.Uom.Id,
                    countedQty = 1m
                }
            }
        };

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-cnt-api-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["ConnectionStrings:DefaultConnection"] = SqliteTestDatabase.CreateConnectionString(_databasePath)
                });
                AuthenticatedApiTestSupport.ConfigureAuthentication(config);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(SqliteTestDatabase.CreateConnectionString(_databasePath)));
                AuthenticatedApiTestSupport.ConfigureServices(services);
            });
        }

        public async Task InitializeAsync()
        {
            await ResetDatabaseAsync();
        }

        public new async Task DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteTestDatabase.DeleteSqliteFileSet(_databasePath);
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

    private sealed record CountReferences(Warehouse Warehouse, Uom Uom, Item Item);

    private sealed record InventoryCountResponse(Guid Id, string CountNo, string Status);

    private sealed record CountListResponse(IReadOnlyList<InventoryCountResponse> Items);
}
