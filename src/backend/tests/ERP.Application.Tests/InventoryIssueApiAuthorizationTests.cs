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

public sealed class InventoryIssueApiAuthorizationTests : IClassFixture<InventoryIssueApiAuthorizationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public InventoryIssueApiAuthorizationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InventoryIssuesApi_ShouldEnforceFeatureGates()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPermissionsAsync(dbContext, Permissions.InventoryIssueView);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        var client = _factory.CreateClient();

        organization.EnableInventory = false;
        organization.EnableInventoryIssues = true;
        await dbContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory-issues")).StatusCode);

        organization.EnableInventory = true;
        organization.EnableInventoryIssues = false;
        await dbContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory-issues")).StatusCode);

        organization.EnableInventoryIssues = true;
        await dbContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory-issues")).StatusCode);
    }

    [Fact]
    public async Task InventoryIssuesApi_ShouldEnforceViewCreateAndPostPermissions()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnableIssuesAsync(dbContext);
        var references = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        await SetPermissionsAsync(dbContext);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/inventory-issues")).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryIssueView);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/inventory-issues")).StatusCode);

        var viewOnlyIssue = await SeedIssueAsync(dbContext, references, DocumentStatus.Draft);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-issues/{viewOnlyIssue.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryIssueCreate);
        var createResponse = await client.PostAsJsonAsync("/api/inventory-issues", CreateRequest(references));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InventoryIssueResponse>();
        Assert.NotNull(created);
        Assert.Equal("ISS-000001", created!.IssueNo);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inventory-issues/{created.Id}/post", null)).StatusCode);

        await SetPermissionsAsync(dbContext, Permissions.InventoryIssuePost);
        var postableIssue = await SeedIssueAsync(dbContext, references, DocumentStatus.Draft);
        await SeedBalanceAsync(dbContext, references, 5m);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/inventory-issues/{postableIssue.Id}/post", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/inventory-issues/{postableIssue.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task InventoryIssuesApi_ShouldHideCrossOrganizationDocuments()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnableIssuesAsync(dbContext);
        await SetPermissionsAsync(dbContext, Permissions.InventoryIssueView, Permissions.InventoryIssueCreate, Permissions.InventoryIssuePost);
        var otherIssue = await SeedOtherOrganizationIssueAsync(dbContext);
        var ownReferences = await SeedReferencesAsync(dbContext);
        var client = _factory.CreateClient();

        var listResponse = await client.GetFromJsonAsync<IssueListResponse>("/api/inventory-issues?search=ISS-OTHER");
        Assert.NotNull(listResponse);
        Assert.Empty(listResponse!.Items);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inventory-issues/{otherIssue}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/inventory-issues/{otherIssue}", CreateRequest(ownReferences))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/inventory-issues/{otherIssue}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-issues/{otherIssue}/post", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/inventory-issues/{otherIssue}/cancel", null)).StatusCode);
    }

    private static async Task EnableIssuesAsync(AppDbContext dbContext)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        organization.EnableInventory = true;
        organization.EnableInventoryIssues = true;
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

    private static async Task<IssueReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var existing = await dbContext.Items.FirstOrDefaultAsync(entity => entity.Code == "ITM-ISS-API");
        if (existing is not null)
        {
            return new IssueReferences(
                await dbContext.Warehouses.SingleAsync(entity => entity.Code == "WH-ISS-API"),
                await dbContext.Uoms.SingleAsync(entity => entity.Code == "PCS-ISS-API"),
                existing);
        }

        var uom = new Uom
        {
            Code = "PCS-ISS-API",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };
        var warehouse = new Warehouse
        {
            Code = "WH-ISS-API",
            Name = "Issue API Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITM-ISS-API",
            Name = "Issue API Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return new IssueReferences(warehouse, uom, item);
    }

    private static async Task<InventoryIssue> SeedIssueAsync(
        AppDbContext dbContext,
        IssueReferences references,
        DocumentStatus status)
    {
        var issue = new InventoryIssue
        {
            IssueNo = $"ISS-API-{Guid.NewGuid():N}"[..24],
            IssueDate = DateTime.UtcNow.Date,
            WarehouseId = references.Warehouse.Id,
            Reason = InventoryIssueReason.InternalConsumption,
            RequestedBy = "API test",
            Notes = "API authorization test",
            Status = status,
            CreatedBy = "seed"
        };
        issue.Lines.Add(new InventoryIssueLine
        {
            LineNo = 1,
            ItemId = references.Item.Id,
            UomId = references.Uom.Id,
            Quantity = 1m,
            BaseQty = 1m,
            CreatedBy = "seed"
        });
        dbContext.InventoryIssues.Add(issue);
        await dbContext.SaveChangesAsync();
        return issue;
    }

    private static async Task SeedBalanceAsync(AppDbContext dbContext, IssueReferences references, decimal baseQty)
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

    private static async Task<Guid> SeedOtherOrganizationIssueAsync(AppDbContext dbContext)
    {
        var organization = new Organization
        {
            Name = $"Other Org {Guid.NewGuid():N}",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            SetupCompleted = true,
            EnableInventory = true,
            EnableInventoryIssues = true,
            CreatedBy = "seed"
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var uom = new Uom { OrganizationId = organization.Id, Code = "PCS-ISS-OTHER", Name = "Pieces", IsActive = true, CreatedBy = "seed" };
        var warehouse = new Warehouse { OrganizationId = organization.Id, Code = "WH-ISS-OTHER", Name = "Other Issue Warehouse", IsActive = true, CreatedBy = "seed" };
        dbContext.Uoms.Add(uom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var item = new Item { OrganizationId = organization.Id, Code = "ITM-ISS-OTHER", Name = "Other Item", BaseUomId = uom.Id, IsActive = true, IsSellable = true, CreatedBy = "seed" };
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        var issue = new InventoryIssue
        {
            OrganizationId = organization.Id,
            IssueNo = "ISS-OTHER-0001",
            IssueDate = DateTime.UtcNow.Date,
            WarehouseId = warehouse.Id,
            Reason = InventoryIssueReason.Damage,
            RequestedBy = "Other org",
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };
        issue.Lines.Add(new InventoryIssueLine
        {
            OrganizationId = organization.Id,
            LineNo = 1,
            ItemId = item.Id,
            UomId = uom.Id,
            Quantity = 1m,
            BaseQty = 1m,
            CreatedBy = "seed"
        });
        dbContext.InventoryIssues.Add(issue);
        await dbContext.SaveChangesAsync();
        return issue.Id;
    }

    private static object CreateRequest(IssueReferences references)
        => new
        {
            issueNo = "CLIENT-CONTROLLED",
            issueDate = DateTime.UtcNow.Date,
            warehouseId = references.Warehouse.Id,
            reason = "InternalConsumption",
            referenceNo = "REQ-API",
            requestedBy = "API test",
            notes = "API test issue",
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
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-iss-api-tests-{Guid.NewGuid():N}.db");

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

    private sealed record IssueReferences(Warehouse Warehouse, Uom Uom, Item Item);

    private sealed record InventoryIssueResponse(Guid Id, string IssueNo, string Status);

    private sealed record IssueListResponse(IReadOnlyList<InventoryIssueResponse> Items);
}
