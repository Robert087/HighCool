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

public sealed class InventoryTransferApiAuthorizationTests : IClassFixture<InventoryTransferApiAuthorizationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public InventoryTransferApiAuthorizationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InventoryTransfersApi_ShouldEnforceFeatureGates()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPermissionsAsync(dbContext, Permissions.InventoryStockLedgerView);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        var client = _factory.CreateClient();

        organization.EnableInventory = false;
        await dbContext.SaveChangesAsync();
        var inventoryDisabled = await client.GetAsync("/api/inventory-transfers");
        Assert.Equal(HttpStatusCode.Forbidden, inventoryDisabled.StatusCode);

        organization.EnableInventory = true;
        organization.EnableStockTransfers = false;
        await dbContext.SaveChangesAsync();
        var transfersDisabled = await client.GetAsync("/api/inventory-transfers");
        Assert.Equal(HttpStatusCode.Forbidden, transfersDisabled.StatusCode);

        organization.EnableStockTransfers = true;
        await dbContext.SaveChangesAsync();
        var enabled = await client.GetAsync("/api/inventory-transfers");
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
    }

    [Fact]
    public async Task InventoryTransfersApi_ShouldEnforceViewCreateAndPostPermissions()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var references = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        await SetPermissionsAsync(dbContext);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory-transfers")).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryStockLedgerView);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory-transfers")).StatusCode);

        var viewOnlyTransfer = await SeedTransferAsync(dbContext, references, DocumentStatus.Draft);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-transfers/{viewOnlyTransfer.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryTransferCreate);
        var createResponse = await client.PostAsJsonAsync("/api/inventory-transfers", CreateRequest(references));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InventoryTransferResponse>();
        Assert.NotNull(created);
        Assert.Equal("TRF-000001", created!.TransferNo);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-transfers/{created.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryTransferPost);
        var postableTransfer = await SeedTransferAsync(dbContext, references, DocumentStatus.Draft);
        await SeedBalanceAsync(dbContext, references, 5m);
        var postResponse = await client.PostAsync($"/api/inventory-transfers/{postableTransfer.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var cancelResponse = await client.PostAsync($"/api/inventory-transfers/{postableTransfer.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task InventoryTransfersApi_ShouldHideCrossOrganizationDocuments()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPermissionsAsync(dbContext, Permissions.InventoryStockLedgerView, Permissions.InventoryTransferCreate, Permissions.InventoryTransferPost);
        var otherTransfer = await SeedOtherOrganizationTransferAsync(dbContext);
        var ownReferences = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        var listResponse = await client.GetFromJsonAsync<TransferListResponse>("/api/inventory-transfers?search=TRF-OTHER");
        Assert.NotNull(listResponse);
        Assert.Empty(listResponse!.Items);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inventory-transfers/{otherTransfer}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/inventory-transfers/{otherTransfer}", CreateRequest(ownReferences))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/inventory-transfers/{otherTransfer}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-transfers/{otherTransfer}/post", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-transfers/{otherTransfer}/cancel", null)).StatusCode);
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

    private static async Task<TransferReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var existing = await dbContext.Items.FirstOrDefaultAsync(entity => entity.Code == "ITM-TRF-API");
        if (existing is not null)
        {
            return new TransferReferences(
                await dbContext.Warehouses.SingleAsync(entity => entity.Code == "SRC-TRF-API"),
                await dbContext.Warehouses.SingleAsync(entity => entity.Code == "DST-TRF-API"),
                await dbContext.Uoms.SingleAsync(entity => entity.Code == "PCS-TRF-API"),
                existing);
        }

        var uom = new Uom
        {
            Code = "PCS-TRF-API",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };
        var sourceWarehouse = new Warehouse
        {
            Code = "SRC-TRF-API",
            Name = "Source Transfer API Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };
        var destinationWarehouse = new Warehouse
        {
            Code = "DST-TRF-API",
            Name = "Destination Transfer API Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.AddRange(sourceWarehouse, destinationWarehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITM-TRF-API",
            Name = "Transfer API Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return new TransferReferences(sourceWarehouse, destinationWarehouse, uom, item);
    }

    private static async Task<InventoryTransfer> SeedTransferAsync(
        AppDbContext dbContext,
        TransferReferences references,
        DocumentStatus status)
    {
        var transfer = new InventoryTransfer
        {
            TransferNo = $"TRF-API-{Guid.NewGuid():N}"[..24],
            TransferDate = DateTime.UtcNow.Date,
            SourceWarehouseId = references.SourceWarehouse.Id,
            DestinationWarehouseId = references.DestinationWarehouse.Id,
            Notes = "API authorization test",
            Status = status,
            CreatedBy = "seed"
        };
        transfer.Lines.Add(new InventoryTransferLine
        {
            LineNo = 1,
            ItemId = references.Item.Id,
            UomId = references.Uom.Id,
            Quantity = 1m,
            BaseQty = 1m,
            CreatedBy = "seed"
        });
        dbContext.InventoryTransfers.Add(transfer);
        await dbContext.SaveChangesAsync();
        return transfer;
    }

    private static async Task SeedBalanceAsync(AppDbContext dbContext, TransferReferences references, decimal baseQty)
    {
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = references.SourceWarehouse.Id,
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

    private static async Task<Guid> SeedOtherOrganizationTransferAsync(AppDbContext dbContext)
    {
        var organization = new Organization
        {
            Name = $"Other Org {Guid.NewGuid():N}",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            SetupCompleted = true,
            EnableInventory = true,
            EnableStockTransfers = true,
            CreatedBy = "seed"
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var uom = new Uom { OrganizationId = organization.Id, Code = "PCS-TRF-OTHER", Name = "Pieces", IsActive = true, CreatedBy = "seed" };
        var sourceWarehouse = new Warehouse { OrganizationId = organization.Id, Code = "SRC-TRF-OTHER", Name = "Other Source Warehouse", IsActive = true, CreatedBy = "seed" };
        var destinationWarehouse = new Warehouse { OrganizationId = organization.Id, Code = "DST-TRF-OTHER", Name = "Other Destination Warehouse", IsActive = true, CreatedBy = "seed" };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.AddRange(sourceWarehouse, destinationWarehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item { OrganizationId = organization.Id, Code = "ITM-TRF-OTHER", Name = "Other Item", BaseUomId = uom.Id, IsActive = true, IsSellable = true, CreatedBy = "seed" };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        var transfer = new InventoryTransfer
        {
            OrganizationId = organization.Id,
            TransferNo = "TRF-OTHER-0001",
            TransferDate = DateTime.UtcNow.Date,
            SourceWarehouseId = sourceWarehouse.Id,
            DestinationWarehouseId = destinationWarehouse.Id,
            Notes = "Other organization",
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };
        transfer.Lines.Add(new InventoryTransferLine
        {
            OrganizationId = organization.Id,
            LineNo = 1,
            ItemId = item.Id,
            UomId = uom.Id,
            Quantity = 1m,
            BaseQty = 1m,
            CreatedBy = "seed"
        });
        dbContext.InventoryTransfers.Add(transfer);
        await dbContext.SaveChangesAsync();
        return transfer.Id;
    }

    private static object CreateRequest(TransferReferences references)
        => new
        {
            transferDate = DateTime.UtcNow.Date,
            sourceWarehouseId = references.SourceWarehouse.Id,
            destinationWarehouseId = references.DestinationWarehouse.Id,
            notes = "API test transfer",
            lines = new[]
            {
                new
                {
                    lineNo = 1,
                    itemId = references.Item.Id,
                    uomId = references.Uom.Id,
                    quantity = 1m
                }
            }
        };

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-trf-api-tests-{Guid.NewGuid():N}.db");

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

    private sealed record TransferReferences(Warehouse SourceWarehouse, Warehouse DestinationWarehouse, Uom Uom, Item Item);

    private sealed record InventoryTransferResponse(Guid Id, string TransferNo, string Status);

    private sealed record TransferListResponse(IReadOnlyList<InventoryTransferResponse> Items);
}
