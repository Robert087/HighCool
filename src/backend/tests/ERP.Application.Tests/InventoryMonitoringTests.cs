using System.Net;
using System.Net.Http.Json;
using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Monitoring;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Inventory.Monitoring;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class InventoryMonitoringTests
{
    [Fact]
    public async Task InventoryMonitoringService_ShouldCalculateStatusesAndSuggestedReorderFromLedger()
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext();
        await using var dbContext = CreateDbContext(executionContext);
        await TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext);
        var references = await SeedReferencesAsync(dbContext);
        var healthy = await SeedItemAsync(dbContext, references, "MON-HEALTHY", "Healthy Item", enableMonitoring: true);
        var low = await SeedItemAsync(dbContext, references, "MON-LOW", "Low Item", enableMonitoring: true);
        var outOfStock = await SeedItemAsync(dbContext, references, "MON-OUT", "Out Item", enableMonitoring: true);
        await SeedBalanceAsync(dbContext, references.Warehouse, healthy, 10m);
        await SeedBalanceAsync(dbContext, references.Warehouse, low, 3m);

        var service = new InventoryMonitoringService(dbContext);

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);
        var rows = await service.ListItemsAsync(
            new InventoryMonitoringListQuery(null, references.Warehouse.Id, null, null, true, 1, 20, "itemCode", SortDirection.Asc),
            CancellationToken.None);

        Assert.Equal(3, dashboard.TotalMonitoredItems);
        Assert.Equal(1, dashboard.HealthyItems);
        Assert.Equal(1, dashboard.LowStockItems);
        Assert.Equal(1, dashboard.OutOfStockItems);

        var healthyRow = Assert.Single(rows.Items.Where(row => row.ItemId == healthy.Id));
        Assert.Equal(InventoryStockStatus.Healthy, healthyRow.Status);
        Assert.Equal(10m, healthyRow.CurrentStock);
        Assert.Equal(10m, healthyRow.SuggestedReorderQuantity);

        var lowRow = Assert.Single(rows.Items.Where(row => row.ItemId == low.Id));
        Assert.Equal(InventoryStockStatus.LowStock, lowRow.Status);
        Assert.Equal(3m, lowRow.CurrentStock);
        Assert.Equal(17m, lowRow.SuggestedReorderQuantity);

        var outRow = Assert.Single(rows.Items.Where(row => row.ItemId == outOfStock.Id));
        Assert.Equal(InventoryStockStatus.OutOfStock, outRow.Status);
        Assert.Equal(0m, outRow.CurrentStock);
        Assert.Equal(20m, outRow.SuggestedReorderQuantity);
    }

    [Fact]
    public async Task InventoryMonitoringService_ShouldFilterSortAndPageOnServer()
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext();
        await using var dbContext = CreateDbContext(executionContext);
        await TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext);
        var references = await SeedReferencesAsync(dbContext);
        var monitoredLow = await SeedItemAsync(dbContext, references, "A-MON-LOW", "Monitored Low", enableMonitoring: true);
        var monitoredHealthy = await SeedItemAsync(dbContext, references, "B-MON-HEALTHY", "Monitored Healthy", enableMonitoring: true);
        await SeedItemAsync(dbContext, references, "C-DISABLED", "Disabled Monitoring", enableMonitoring: false);
        await SeedBalanceAsync(dbContext, references.Warehouse, monitoredLow, 2m);
        await SeedBalanceAsync(dbContext, references.Warehouse, monitoredHealthy, 9m);

        var service = new InventoryMonitoringService(dbContext);

        var lowRows = await service.ListItemsAsync(
            new InventoryMonitoringListQuery("MON", references.Warehouse.Id, references.Category.Id, InventoryStockStatus.LowStock, true, 1, 10, "currentStock", SortDirection.Asc),
            CancellationToken.None);
        var allRows = await service.ListItemsAsync(
            new InventoryMonitoringListQuery(null, references.Warehouse.Id, null, null, false, 2, 1, "itemCode", SortDirection.Asc),
            CancellationToken.None);

        var lowRow = Assert.Single(lowRows.Items);
        Assert.Equal(monitoredLow.Id, lowRow.ItemId);
        Assert.Equal(1, lowRows.TotalCount);
        Assert.Equal(3, allRows.TotalCount);
        Assert.Equal(2, allRows.Page);
        Assert.Equal(3, allRows.TotalPages);
    }

    [Fact]
    public async Task InventoryMonitoringService_ShouldUpdateReorderSettingsAndValidateRequest()
    {
        var validator = new UpdateReorderSettingsRequestValidator();
        var invalid = await validator.ValidateAsync(new UpdateReorderSettingsRequest(
            EnableMonitoring: true,
            MinimumStock: 5m,
            ReorderPoint: 4m,
            MaximumStock: 0m,
            ReorderQuantity: 0m,
            SafetyStock: -1m,
            LeadTimeDays: -1));
        Assert.False(invalid.IsValid);

        var executionContext = TestOrganizationContext.CreateExecutionContext();
        await using var dbContext = CreateDbContext(executionContext);
        await TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext);
        var references = await SeedReferencesAsync(dbContext);
        var item = await SeedItemAsync(dbContext, references, "SETTINGS", "Settings Item", enableMonitoring: false);
        var service = new InventoryMonitoringService(dbContext);

        var updated = await service.UpdateReorderSettingsAsync(
            item.Id,
            new UpdateReorderSettingsRequest(true, 2m, 5m, 20m, 8m, 1m, 4),
            "tester",
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.True(updated!.EnableMonitoring);
        Assert.Equal(2m, updated.MinimumStock);
        Assert.Equal(5m, updated.ReorderPoint);
        Assert.Equal(20m, updated.MaximumStock);
        Assert.Equal(8m, updated.ReorderQuantity);
        Assert.Equal(1m, updated.SafetyStock);
        Assert.Equal(4, updated.LeadTimeDays);
    }

    [Fact]
    public async Task InventoryMonitoringService_ShouldApplyBoundaryRulesAcrossWarehousesAndMovements()
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext();
        await using var dbContext = CreateDbContext(executionContext);
        await TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext);
        var references = await SeedReferencesAsync(dbContext);
        var secondaryWarehouse = await SeedWarehouseAsync(dbContext, "WH-SECOND");
        var boundaryItem = await SeedItemAsync(dbContext, references, "MON-BOUND", "Boundary Item", enableMonitoring: true);
        var healthyItem = await SeedItemAsync(dbContext, references, "MON-MOVE", "Movement Item", enableMonitoring: true);
        await SeedItemAsync(dbContext, references, "MON-DISABLED", "Disabled Item", enableMonitoring: false);
        await SeedLedgerEntryAsync(dbContext, references.Warehouse, boundaryItem, 10m, 0m, StockTransactionType.PurchaseReceipt, SourceDocumentType.PurchaseReceipt);
        await SeedLedgerEntryAsync(dbContext, references.Warehouse, boundaryItem, 0m, 5m, StockTransactionType.InventoryAdjustmentDecrease, SourceDocumentType.InventoryAdjustment);
        await SeedLedgerEntryAsync(dbContext, secondaryWarehouse, boundaryItem, 0m, 1m, StockTransactionType.InventoryIssue, SourceDocumentType.InventoryIssue);
        await SeedLedgerEntryAsync(dbContext, secondaryWarehouse, healthyItem, 4m, 0m, StockTransactionType.InventoryTransferIn, SourceDocumentType.InventoryTransfer);
        await SeedLedgerEntryAsync(dbContext, secondaryWarehouse, healthyItem, 5m, 0m, StockTransactionType.InventoryCountIncrease, SourceDocumentType.InventoryCount);

        var service = new InventoryMonitoringService(dbContext);

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);
        var allRows = await service.ListItemsAsync(
            new InventoryMonitoringListQuery(null, null, null, null, true, 1, 20, "itemCode", SortDirection.Asc),
            CancellationToken.None);
        var disabledRows = await service.ListItemsAsync(
            new InventoryMonitoringListQuery(null, secondaryWarehouse.Id, null, InventoryStockStatus.NotMonitored, false, 1, 20, "itemCode", SortDirection.Asc),
            CancellationToken.None);

        Assert.Equal(dashboard.TotalMonitoredItems, dashboard.HealthyItems + dashboard.LowStockItems + dashboard.OutOfStockItems);
        Assert.Equal(4, dashboard.TotalMonitoredItems);
        Assert.Equal(1, dashboard.HealthyItems);
        Assert.Equal(1, dashboard.LowStockItems);
        Assert.Equal(2, dashboard.OutOfStockItems);

        var exactReorderPoint = Assert.Single(allRows.Items.Where(row => row.ItemId == boundaryItem.Id && row.WarehouseId == references.Warehouse.Id));
        Assert.Equal(5m, exactReorderPoint.CurrentStock);
        Assert.Equal(InventoryStockStatus.LowStock, exactReorderPoint.Status);
        Assert.Equal(15m, exactReorderPoint.SuggestedReorderQuantity);

        var negativeStock = Assert.Single(allRows.Items.Where(row => row.ItemId == boundaryItem.Id && row.WarehouseId == secondaryWarehouse.Id));
        Assert.Equal(-1m, negativeStock.CurrentStock);
        Assert.Equal(InventoryStockStatus.OutOfStock, negativeStock.Status);
        Assert.Equal(21m, negativeStock.SuggestedReorderQuantity);

        var healthy = Assert.Single(allRows.Items.Where(row => row.ItemId == healthyItem.Id && row.WarehouseId == secondaryWarehouse.Id));
        Assert.Equal(9m, healthy.CurrentStock);
        Assert.Equal(InventoryStockStatus.Healthy, healthy.Status);
        Assert.Equal(11m, healthy.SuggestedReorderQuantity);

        var disabled = Assert.Single(disabledRows.Items);
        Assert.False(disabled.EnableMonitoring);
        Assert.Equal(InventoryStockStatus.NotMonitored, disabled.Status);
    }

    [Fact]
    public async Task InventoryMonitoringValidation_ShouldAllowBoundariesAndOptionalNulls()
    {
        var validator = new UpdateReorderSettingsRequestValidator();

        var valid = await validator.ValidateAsync(new UpdateReorderSettingsRequest(
            EnableMonitoring: true,
            MinimumStock: 5m,
            ReorderPoint: 5m,
            MaximumStock: 5m,
            ReorderQuantity: 0.000001m,
            SafetyStock: null,
            LeadTimeDays: null));
        var validDisabled = await validator.ValidateAsync(new UpdateReorderSettingsRequest(
            EnableMonitoring: false,
            MinimumStock: 0m,
            ReorderPoint: 0m,
            MaximumStock: 1m,
            ReorderQuantity: 1m,
            SafetyStock: 0m,
            LeadTimeDays: 0));
        var invalidDisabled = await validator.ValidateAsync(new UpdateReorderSettingsRequest(
            EnableMonitoring: false,
            MinimumStock: -1m,
            ReorderPoint: 0m,
            MaximumStock: 0m,
            ReorderQuantity: 0m,
            SafetyStock: -0.000001m,
            LeadTimeDays: -1));

        Assert.True(valid.IsValid);
        Assert.True(validDisabled.IsValid);
        Assert.False(invalidDisabled.IsValid);
    }

    private static AppDbContext CreateDbContext(TestRequestExecutionContext executionContext)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-monitoring-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(SqliteTestDatabase.CreateConnectionString(databasePath))
            .Options;

        var dbContext = new AppDbContext(options, executionContext);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<MonitoringReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var uom = new Uom
        {
            Code = $"PCS-{Guid.NewGuid():N}"[..12],
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };
        var warehouse = new Warehouse
        {
            Code = $"WH-{Guid.NewGuid():N}"[..12],
            Name = "Main Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };
        var category = new ItemCategory
        {
            Code = $"CAT-{Guid.NewGuid():N}"[..12],
            Name = "Monitoring Category",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        dbContext.ItemCategories.Add(category);
        await dbContext.SaveChangesAsync();

        return new MonitoringReferences(uom, warehouse, category);
    }

    private static async Task<Item> SeedItemAsync(
        AppDbContext dbContext,
        MonitoringReferences references,
        string code,
        string name,
        bool enableMonitoring)
    {
        var item = new Item
        {
            Code = code,
            Name = name,
            CategoryId = references.Category.Id,
            BaseUomId = references.Uom.Id,
            DefaultWarehouseId = references.Warehouse.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            EnableInventoryMonitoring = enableMonitoring,
            MinimumStockQuantity = 2m,
            ReorderPointQuantity = enableMonitoring ? 5m : null,
            MaximumStockQuantity = enableMonitoring ? 20m : null,
            ReorderQuantity = enableMonitoring ? 8m : null,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();
        return item;
    }

    private static async Task SeedBalanceAsync(
        AppDbContext dbContext,
        Warehouse warehouse,
        Item item,
        decimal baseQty)
    {
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = baseQty,
            QtyOut = 0m,
            UomId = item.BaseUomId,
            BaseQty = baseQty,
            RunningBalanceQty = baseQty,
            TransactionDate = DateTime.UtcNow.Date,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Warehouse> SeedWarehouseAsync(AppDbContext dbContext, string code)
    {
        var warehouse = new Warehouse
        {
            Code = $"{code}-{Guid.NewGuid():N}"[..12],
            Name = code,
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();
        return warehouse;
    }

    private static async Task SeedLedgerEntryAsync(
        AppDbContext dbContext,
        Warehouse warehouse,
        Item item,
        decimal qtyIn,
        decimal qtyOut,
        StockTransactionType transactionType,
        SourceDocumentType sourceDocumentType)
    {
        var baseQty = qtyIn > 0m ? qtyIn : qtyOut;
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            TransactionType = transactionType,
            SourceDocType = sourceDocumentType,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = qtyIn,
            QtyOut = qtyOut,
            UomId = item.BaseUomId,
            BaseQty = baseQty,
            RunningBalanceQty = qtyIn - qtyOut,
            TransactionDate = DateTime.UtcNow.Date,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed record MonitoringReferences(Uom Uom, Warehouse Warehouse, ItemCategory Category);
}

public sealed class InventoryMonitoringApiTests : IClassFixture<InventoryMonitoringApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public InventoryMonitoringApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InventoryMonitoringApi_ShouldEnforceFeatureGates()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPermissionsAsync(dbContext, Permissions.InventoryMonitorView);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        var client = _factory.CreateClient();

        organization.EnableInventory = false;
        organization.EnableLowStockAlerts = true;
        await dbContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory/monitor/dashboard")).StatusCode);

        organization.EnableInventory = true;
        organization.EnableLowStockAlerts = false;
        await dbContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory/monitor/dashboard")).StatusCode);

        organization.EnableLowStockAlerts = true;
        await dbContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory/monitor/dashboard")).StatusCode);
    }

    [Fact]
    public async Task InventoryMonitoringApi_ShouldEnforceViewAndManagePermissions()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnableMonitoringAsync(dbContext);
        var item = await SeedApiItemAsync(dbContext);
        var client = _factory.CreateClient();

        await SetPermissionsAsync(dbContext);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory/monitor/items")).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryMonitorView);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory/monitor/items")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory/monitor/filter-options")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inventory/items/{item.Id}/reorder-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/inventory/items/{item.Id}/reorder-settings", CreateSettingsPayload())).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryMonitorManage);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/inventory/items/{item.Id}/reorder-settings", CreateSettingsPayload())).StatusCode);
    }

    [Fact]
    public async Task InventoryMonitoringApi_ShouldHideCrossOrganizationItems()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnableMonitoringAsync(dbContext);
        await SetPermissionsAsync(dbContext, Permissions.InventoryMonitorView, Permissions.InventoryMonitorManage);
        var ownItem = await SeedApiItemAsync(dbContext);
        var otherItemId = await SeedOtherOrganizationItemAsync(dbContext);
        var client = _factory.CreateClient();

        var listResponse = await client.GetFromJsonAsync<MonitoringListResponse>("/api/inventory/monitor/items?onlyMonitored=false&search=OTHER-MONITOR");
        Assert.NotNull(listResponse);
        Assert.Empty(listResponse!.Items);

        var dashboard = await client.GetFromJsonAsync<InventoryMonitoringDashboardDto>("/api/inventory/monitor/dashboard");
        Assert.NotNull(dashboard);
        Assert.Equal(0, dashboard!.TotalMonitoredItems);

        var filterOptions = await client.GetFromJsonAsync<InventoryMonitoringFilterOptionsDto>("/api/inventory/monitor/filter-options");
        Assert.NotNull(filterOptions);
        Assert.DoesNotContain(filterOptions!.Warehouses, option => option.Code == "WH-OTHER-M");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inventory/items/{otherItemId}/reorder-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/inventory/items/{otherItemId}/reorder-settings", CreateSettingsPayload())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inventory/items/{ownItem.Id}/reorder-settings")).StatusCode);
    }

    [Fact]
    public async Task InventoryMonitoringApi_ShouldRejectInvalidSettingsPayload()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnableMonitoringAsync(dbContext);
        await SetPermissionsAsync(dbContext, Permissions.InventoryMonitorManage);
        var item = await SeedApiItemAsync(dbContext);
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/inventory/items/{item.Id}/reorder-settings",
            new
            {
                enableMonitoring = true,
                minimumStock = 5m,
                reorderPoint = 4m,
                maximumStock = 0m,
                reorderQuantity = 0m,
                safetyStock = -1m,
                leadTimeDays = -1
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task EnableMonitoringAsync(AppDbContext dbContext)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        organization.EnableInventory = true;
        organization.EnableLowStockAlerts = true;
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
            Name = $"Monitoring role {Guid.NewGuid():N}",
            IsActive = true,
            CreatedBy = "test"
        };
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        dbContext.RolePermissions.AddRange(ERP.Application.Security.Permissions.Expand(permissions).Distinct(StringComparer.OrdinalIgnoreCase).Select(permission => new RolePermission
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

    private static async Task<Item> SeedApiItemAsync(AppDbContext dbContext)
    {
        var uom = new Uom { Code = $"UOM-{Guid.NewGuid():N}"[..12], Name = "Pieces", IsActive = true, CreatedBy = "seed" };
        var warehouse = new Warehouse { Code = $"WH-{Guid.NewGuid():N}"[..12], Name = "Monitoring Warehouse", IsActive = true, CreatedBy = "seed" };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = $"MON-{Guid.NewGuid():N}"[..12],
            Name = "Monitoring API Item",
            BaseUomId = uom.Id,
            DefaultWarehouseId = warehouse.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();
        return item;
    }

    private static async Task<Guid> SeedOtherOrganizationItemAsync(AppDbContext dbContext)
    {
        var organization = new Organization
        {
            Name = $"Other Org {Guid.NewGuid():N}",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            SetupCompleted = true,
            EnableInventory = true,
            EnableLowStockAlerts = true,
            CreatedBy = "seed"
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var uom = new Uom { OrganizationId = organization.Id, Code = "UOM-OTHER-M", Name = "Pieces", IsActive = true, CreatedBy = "seed" };
        var warehouse = new Warehouse { OrganizationId = organization.Id, Code = "WH-OTHER-M", Name = "Other Warehouse", IsActive = true, CreatedBy = "seed" };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            OrganizationId = organization.Id,
            Code = "OTHER-MONITOR",
            Name = "Other Monitoring Item",
            BaseUomId = uom.Id,
            DefaultWarehouseId = warehouse.Id,
            IsActive = true,
            IsSellable = true,
            EnableInventoryMonitoring = true,
            MinimumStockQuantity = 1m,
            ReorderPointQuantity = 2m,
            MaximumStockQuantity = 5m,
            ReorderQuantity = 3m,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();
        return item.Id;
    }

    private static object CreateSettingsPayload()
        => new
        {
            enableMonitoring = true,
            minimumStock = 2m,
            reorderPoint = 5m,
            maximumStock = 20m,
            reorderQuantity = 8m,
            safetyStock = 1m,
            leadTimeDays = 4
        };

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-monitoring-api-tests-{Guid.NewGuid():N}.db");

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

    private sealed record MonitoringListResponse(IReadOnlyList<InventoryMonitoringItemDto> Items);
}
