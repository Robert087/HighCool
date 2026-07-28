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

public sealed class InventoryAdjustmentApiAuthorizationTests : IClassFixture<InventoryAdjustmentApiAuthorizationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public InventoryAdjustmentApiAuthorizationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InventoryAdjustmentsApi_ShouldEnforceFeatureGates()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPermissionsAsync(dbContext, Permissions.InventoryStockLedgerView);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        var client = _factory.CreateClient();

        organization.EnableInventory = false;
        await dbContext.SaveChangesAsync();
        var inventoryDisabled = await client.GetAsync("/api/inventory-adjustments");
        Assert.Equal(HttpStatusCode.Forbidden, inventoryDisabled.StatusCode);

        organization.EnableInventory = true;
        organization.EnableStockAdjustments = false;
        await dbContext.SaveChangesAsync();
        var adjustmentsDisabled = await client.GetAsync("/api/inventory-adjustments");
        Assert.Equal(HttpStatusCode.Forbidden, adjustmentsDisabled.StatusCode);

        organization.EnableStockAdjustments = true;
        await dbContext.SaveChangesAsync();
        var enabled = await client.GetAsync("/api/inventory-adjustments");
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
    }

    [Fact]
    public async Task InventoryAdjustmentsApi_ShouldEnforceViewCreateAndPostPermissions()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var references = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        await SetPermissionsAsync(dbContext);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory-adjustments")).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryStockLedgerView);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory-adjustments")).StatusCode);

        var viewOnlyAdjustment = await SeedAdjustmentAsync(dbContext, references, DocumentStatus.Draft);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-adjustments/{viewOnlyAdjustment.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryAdjustmentCreate);
        var createResponse = await client.PostAsJsonAsync("/api/inventory-adjustments", CreateRequest(references));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InventoryAdjustmentResponse>();
        Assert.NotNull(created);
        Assert.Equal("ADJ-000001", created!.AdjustmentNo);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-adjustments/{created.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryAdjustmentPost);
        var postableAdjustment = await SeedAdjustmentAsync(dbContext, references, DocumentStatus.Draft);
        var postResponse = await client.PostAsync($"/api/inventory-adjustments/{postableAdjustment.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var cancelResponse = await client.PostAsync($"/api/inventory-adjustments/{postableAdjustment.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task InventoryAdjustmentsApi_ShouldHideCrossOrganizationDocuments()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPermissionsAsync(dbContext, Permissions.InventoryStockLedgerView, Permissions.InventoryAdjustmentCreate, Permissions.InventoryAdjustmentPost);
        var otherAdjustment = await SeedOtherOrganizationAdjustmentAsync(dbContext);
        var ownReferences = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inventory-adjustments/{otherAdjustment}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/inventory-adjustments/{otherAdjustment}", CreateRequest(ownReferences))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/inventory-adjustments/{otherAdjustment}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-adjustments/{otherAdjustment}/post", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-adjustments/{otherAdjustment}/cancel", null)).StatusCode);
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

    private static async Task<AdjustmentReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var existing = await dbContext.Items.FirstOrDefaultAsync(entity => entity.Code == "ITM-ADJ-API");
        if (existing is not null)
        {
            return new AdjustmentReferences(
                await dbContext.Warehouses.SingleAsync(entity => entity.Code == "MAIN-ADJ-API"),
                await dbContext.Uoms.SingleAsync(entity => entity.Code == "PCS-ADJ-API"),
                existing);
        }

        var uom = new Uom
        {
            Code = "PCS-ADJ-API",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };
        var warehouse = new Warehouse
        {
            Code = "MAIN-ADJ-API",
            Name = "Main Adjustment API Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITM-ADJ-API",
            Name = "Adjustment API Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return new AdjustmentReferences(warehouse, uom, item);
    }

    private static async Task<InventoryAdjustment> SeedAdjustmentAsync(
        AppDbContext dbContext,
        AdjustmentReferences references,
        DocumentStatus status)
    {
        var adjustment = new InventoryAdjustment
        {
            AdjustmentNo = $"ADJ-API-{Guid.NewGuid():N}"[..24],
            AdjustmentDate = DateTime.UtcNow.Date,
            WarehouseId = references.Warehouse.Id,
            Reason = "API authorization test",
            Status = status,
            CreatedBy = "seed"
        };
        adjustment.Lines.Add(new InventoryAdjustmentLine
        {
            LineNo = 1,
            ItemId = references.Item.Id,
            UomId = references.Uom.Id,
            Quantity = 1m,
            BaseQty = 1m,
            AdjustmentType = InventoryAdjustmentType.Increase,
            CreatedBy = "seed"
        });
        dbContext.InventoryAdjustments.Add(adjustment);
        await dbContext.SaveChangesAsync();
        return adjustment;
    }

    private static async Task<Guid> SeedOtherOrganizationAdjustmentAsync(AppDbContext dbContext)
    {
        var organization = new Organization
        {
            Name = $"Other Org {Guid.NewGuid():N}",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            SetupCompleted = true,
            EnableInventory = true,
            EnableStockAdjustments = true,
            CreatedBy = "seed"
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var uom = new Uom { OrganizationId = organization.Id, Code = "PCS-OTHER", Name = "Pieces", IsActive = true, CreatedBy = "seed" };
        var warehouse = new Warehouse { OrganizationId = organization.Id, Code = "OTHER", Name = "Other Warehouse", IsActive = true, CreatedBy = "seed" };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item { OrganizationId = organization.Id, Code = "ITM-OTHER", Name = "Other Item", BaseUomId = uom.Id, IsActive = true, IsSellable = true, CreatedBy = "seed" };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        var adjustment = new InventoryAdjustment
        {
            OrganizationId = organization.Id,
            AdjustmentNo = "ADJ-OTHER-0001",
            AdjustmentDate = DateTime.UtcNow.Date,
            WarehouseId = warehouse.Id,
            Reason = "Other organization",
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };
        adjustment.Lines.Add(new InventoryAdjustmentLine
        {
            OrganizationId = organization.Id,
            LineNo = 1,
            ItemId = item.Id,
            UomId = uom.Id,
            Quantity = 1m,
            BaseQty = 1m,
            AdjustmentType = InventoryAdjustmentType.Increase,
            CreatedBy = "seed"
        });
        dbContext.InventoryAdjustments.Add(adjustment);
        await dbContext.SaveChangesAsync();
        return adjustment.Id;
    }

    private static object CreateRequest(AdjustmentReferences references)
        => new
        {
            adjustmentDate = DateTime.UtcNow.Date,
            warehouseId = references.Warehouse.Id,
            reason = "API test adjustment",
            lines = new[]
            {
                new
                {
                    lineNo = 1,
                    itemId = references.Item.Id,
                    uomId = references.Uom.Id,
                    quantity = 1m,
                    adjustmentType = "Increase"
                }
            }
        };

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-adj-api-tests-{Guid.NewGuid():N}.db");

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

    private sealed record AdjustmentReferences(Warehouse Warehouse, Uom Uom, Item Item);

    private sealed record InventoryAdjustmentResponse(Guid Id, string AdjustmentNo, string Status);
}
