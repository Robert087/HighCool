using System.Net;
using System.Net.Http.Json;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Pricing;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Domain.MasterData;
using ERP.Domain.Pricing;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Pricing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class PricingTests : IClassFixture<PricingTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public PricingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PriceLists_ShouldEnforceDefaultRulesAndOptimisticConcurrency()
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext();
        await using var dbContext = CreateDbContext(executionContext);
        await TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext);
        var service = new PricingService(dbContext, executionContext);

        var standard = await service.CreatePriceListAsync(
            new UpsertPriceListRequest("sell-std", "Standard Selling", PriceListType.Selling, "egp", true, true, null),
            "tester",
            CancellationToken.None);
        var wholesale = await service.CreatePriceListAsync(
            new UpsertPriceListRequest("sell-wh", "Wholesale", PriceListType.Selling, "EGP", true, true, null),
            "tester",
            CancellationToken.None);
        var buying = await service.CreatePriceListAsync(
            new UpsertPriceListRequest("buy-std", "Standard Buying", PriceListType.Buying, "EGP", true, true, null),
            "tester",
            CancellationToken.None);

        var refreshedStandard = await service.GetPriceListAsync(standard.Id, CancellationToken.None);

        Assert.Equal("SELL-STD", standard.Code);
        Assert.False(refreshedStandard!.IsDefault);
        Assert.True(wholesale.IsDefault);
        Assert.True(buying.IsDefault);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePriceListAsync(
            new UpsertPriceListRequest("sell-bad", "Inactive Default", PriceListType.Selling, "EGP", true, false, null),
            "tester",
            CancellationToken.None));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.UpdatePriceListAsync(
            wholesale.Id,
            new UpdatePriceListRequest(wholesale.Code, wholesale.Name, wholesale.Type, wholesale.Currency, wholesale.IsDefault, wholesale.IsActive, wholesale.Description, wholesale.Version - 1),
            "tester",
            CancellationToken.None));

        var deactivated = await service.DeactivatePriceListAsync(wholesale.Id, wholesale.Version, "tester", CancellationToken.None);
        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsDefault);
        Assert.False(deactivated.IsActive);
    }

    [Fact]
    public async Task ItemPrices_ShouldValidateOverlapUomCurrencyAndResolveTiers()
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext();
        await using var dbContext = CreateDbContext(executionContext);
        await TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext);
        var references = await SeedReferencesAsync(dbContext);
        var service = new PricingService(dbContext, executionContext);
        var priceList = await service.CreatePriceListAsync(
            new UpsertPriceListRequest("sell-std", "Standard Selling", PriceListType.Selling, "EGP", true, true, null),
            "tester",
            CancellationToken.None);

        var baseTier = await service.CreateItemPriceAsync(
            new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.Piece.Id, "EGP", 10m, 1m, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), true, null),
            "tester",
            CancellationToken.None);
        await service.CreateItemPriceAsync(
            new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.Piece.Id, null, 9m, 10m, new DateTime(2026, 1, 1), null, true, null),
            "tester",
            CancellationToken.None);
        await service.CreateItemPriceAsync(
            new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.Box.Id, null, 95m, 1m, new DateTime(2026, 1, 1), null, true, null),
            "tester",
            CancellationToken.None);
        var adjacent = await service.CreateItemPriceAsync(
            new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.Piece.Id, null, 11m, 1m, new DateTime(2026, 7, 1), null, true, null),
            "tester",
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<DuplicateEntityException>(() => service.CreateItemPriceAsync(
            new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.Piece.Id, null, 12m, 1m, new DateTime(2026, 6, 1), null, true, null),
            "tester",
            CancellationToken.None));
        Assert.Contains("overlapping", conflict.Message, StringComparison.OrdinalIgnoreCase);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateItemPriceAsync(
            new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.InvalidUom.Id, null, 10m, 1m, new DateTime(2026, 1, 1), null, true, null),
            "tester",
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateItemPriceAsync(
            new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.Piece.Id, "USD", 10m, 100m, new DateTime(2026, 1, 1), null, true, null),
            "tester",
            CancellationToken.None));

        var tier = await service.ResolvePriceAsync(
            new PriceResolutionQuery(priceList.Id, references.Item.Id, references.Piece.Id, 25m, new DateTime(2026, 2, 1)),
            CancellationToken.None);
        var belowTier = await service.ResolvePriceAsync(
            new PriceResolutionQuery(priceList.Id, references.Item.Id, references.Piece.Id, 0.5m, new DateTime(2026, 2, 1)),
            CancellationToken.None);
        var adjacentResult = await service.ResolvePriceAsync(
            new PriceResolutionQuery(priceList.Id, references.Item.Id, references.Piece.Id, 1m, new DateTime(2026, 8, 1)),
            CancellationToken.None);

        Assert.Equal(9m, tier!.Rate);
        Assert.Null(belowTier);
        Assert.Equal(adjacent.Id, adjacentResult!.ItemPriceId);
        Assert.Equal("EGP", baseTier.Currency);
    }

    [Fact]
    public async Task PricingApi_ShouldEnforceFeatureAndPermissionGates()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var allowed = await client.GetAsync("/api/pricing/price-lists");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
            organization.EnablePriceLists = false;
            await dbContext.SaveChangesAsync();
        }

        var featureBlocked = await client.GetAsync("/api/pricing/price-lists");
        Assert.Equal(HttpStatusCode.Forbidden, featureBlocked.StatusCode);

        await _factory.ResetDatabaseAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SetPermissionsAsync(dbContext, Permissions.PricingPriceListView);
        }

        var createBlocked = await client.PostAsJsonAsync("/api/pricing/price-lists", new
        {
            code = "SELL-STD",
            name = "Standard Selling",
            type = "Selling",
            currency = "EGP",
            isDefault = true,
            isActive = true,
            description = ""
        });
        Assert.Equal(HttpStatusCode.Forbidden, createBlocked.StatusCode);
    }

    [Fact]
    public async Task PriceLists_ShouldScopeCodeUniquenessAndNormalizeDuplicates()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var firstContext = TestOrganizationContext.CreateExecutionContext();
            await using (var dbContext = CreateDbContext(databasePath, firstContext))
            {
                await SeedOrganizationAsync(dbContext, firstContext, "First Organization");
                var service = new PricingService(dbContext, firstContext);
                await service.CreatePriceListAsync(
                    new UpsertPriceListRequest(" sell-std ", "Standard", PriceListType.Selling, "egp", false, true, null),
                    "tester",
                    CancellationToken.None);

                await Assert.ThrowsAsync<DuplicateEntityException>(() => service.CreatePriceListAsync(
                    new UpsertPriceListRequest("SELL-STD", "Duplicate", PriceListType.Selling, "EGP", false, true, null),
                    "tester",
                    CancellationToken.None));
            }

            var secondContext = TestOrganizationContext.CreateExecutionContext();
            await using (var dbContext = CreateDbContext(databasePath, secondContext))
            {
                await SeedOrganizationAsync(dbContext, secondContext, "Second Organization");
                var service = new PricingService(dbContext, secondContext);
                var result = await service.CreatePriceListAsync(
                    new UpsertPriceListRequest("SELL-STD", "Standard", PriceListType.Selling, "EGP", false, true, null),
                    "tester",
                    CancellationToken.None);

                Assert.Equal("SELL-STD", result.Code);
            }
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task ItemPrices_ShouldEnforceOrganizationIsolationCurrencySyncAndDateBoundaries()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            Guid firstPriceListId;
            Guid firstItemId;
            Guid firstUomId;

            var firstContext = TestOrganizationContext.CreateExecutionContext();
            await using (var dbContext = CreateDbContext(databasePath, firstContext))
            {
                await SeedOrganizationAsync(dbContext, firstContext, "First Organization");
                var references = await SeedReferencesAsync(dbContext);
                var service = new PricingService(dbContext, firstContext);
                var priceList = await service.CreatePriceListAsync(
                    new UpsertPriceListRequest("SELL-STD", "Standard", PriceListType.Selling, "EGP", true, true, null),
                    "tester",
                    CancellationToken.None);
                await service.CreateItemPriceAsync(
                    new UpsertItemPriceRequest(priceList.Id, references.Item.Id, references.Piece.Id, null, 10m, 1m, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), true, null),
                    "tester",
                    CancellationToken.None);

                var boundaryStart = await service.ResolvePriceAsync(new PriceResolutionQuery(priceList.Id, references.Item.Id, references.Piece.Id, 1m, new DateTime(2026, 1, 1)), CancellationToken.None);
                var boundaryEnd = await service.ResolvePriceAsync(new PriceResolutionQuery(priceList.Id, references.Item.Id, references.Piece.Id, 1m, new DateTime(2026, 1, 31)), CancellationToken.None);
                var expired = await service.ResolvePriceAsync(new PriceResolutionQuery(priceList.Id, references.Item.Id, references.Piece.Id, 1m, new DateTime(2026, 2, 1)), CancellationToken.None);

                Assert.NotNull(boundaryStart);
                Assert.NotNull(boundaryEnd);
                Assert.Null(expired);

                var updated = await service.UpdatePriceListAsync(
                    priceList.Id,
                    new UpdatePriceListRequest(priceList.Code, priceList.Name, priceList.Type, "USD", priceList.IsDefault, priceList.IsActive, priceList.Description, priceList.Version),
                    "tester",
                    CancellationToken.None);
                var synced = await service.ListItemPricesAsync(new ItemPriceListQuery(null, priceList.Id, null, null, null, null, null, null, null, null, null), CancellationToken.None);

                Assert.Equal("USD", updated!.Currency);
                Assert.All(synced.Items, itemPrice => Assert.Equal("USD", itemPrice.Currency));

                firstPriceListId = priceList.Id;
                firstItemId = references.Item.Id;
                firstUomId = references.Piece.Id;
            }

            var secondContext = TestOrganizationContext.CreateExecutionContext();
            await using (var dbContext = CreateDbContext(databasePath, secondContext))
            {
                await SeedOrganizationAsync(dbContext, secondContext, "Second Organization");
                var service = new PricingService(dbContext, secondContext);

                Assert.Null(await service.GetPriceListAsync(firstPriceListId, CancellationToken.None));
                Assert.Null(await service.GetItemPriceAsync(firstPriceListId, CancellationToken.None));
                Assert.Null(await service.ResolvePriceAsync(new PriceResolutionQuery(firstPriceListId, firstItemId, firstUomId, 1m, new DateTime(2026, 1, 1)), CancellationToken.None));
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateItemPriceAsync(
                    new UpsertItemPriceRequest(firstPriceListId, firstItemId, firstUomId, null, 10m, 1m, new DateTime(2026, 1, 1), null, true, null),
                    "tester",
                    CancellationToken.None));
            }
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task Pricing_ShouldKeepSafeFinalStateUnderConcurrentDefaultAndOverlapAttempts()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            Guid organizationId;
            Guid priceListId;
            Guid itemId;
            Guid uomId;

            var setupContext = TestOrganizationContext.CreateExecutionContext();
            await using (var dbContext = CreateDbContext(databasePath, setupContext))
            {
                organizationId = await SeedOrganizationAsync(dbContext, setupContext, "Concurrent Organization");
                var references = await SeedReferencesAsync(dbContext);
                var service = new PricingService(dbContext, setupContext);
                var priceList = await service.CreatePriceListAsync(
                    new UpsertPriceListRequest("SELL-BASE", "Base", PriceListType.Selling, "EGP", false, true, null),
                    "tester",
                    CancellationToken.None);

                priceListId = priceList.Id;
                itemId = references.Item.Id;
                uomId = references.Piece.Id;
            }

            var defaultTasks = Enumerable.Range(1, 6)
                .Select(index => RunPricingAttemptAsync(databasePath, organizationId, async service =>
                {
                    await service.CreatePriceListAsync(
                        new UpsertPriceListRequest($"SELL-D{index}", $"Default {index}", PriceListType.Selling, "EGP", true, true, null),
                        "tester",
                        CancellationToken.None);
                }))
                .ToArray();

            await Task.WhenAll(defaultTasks);

            var overlapTasks = Enumerable.Range(1, 6)
                .Select(index => RunPricingAttemptAsync(databasePath, organizationId, async service =>
                {
                    await service.CreateItemPriceAsync(
                        new UpsertItemPriceRequest(priceListId, itemId, uomId, null, 10m + index, 1m, new DateTime(2026, 1, 1), null, true, null),
                        "tester",
                        CancellationToken.None);
                }))
                .ToArray();

            await Task.WhenAll(overlapTasks);

            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
            var activeSellingDefaults = await verifyContext.PriceLists.CountAsync(entity => entity.Type == PriceListType.Selling && entity.IsActive && entity.IsDefault);
            var activeOverlappingPrices = await verifyContext.ItemPrices.CountAsync(entity =>
                entity.PriceListId == priceListId &&
                entity.ItemId == itemId &&
                entity.UomId == uomId &&
                entity.MinimumQuantity == 1m &&
                entity.IsActive);

            Assert.Equal(1, activeSellingDefaults);
            Assert.Equal(1, activeOverlappingPrices);
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task PricingFilterOptions_ShouldReturnOnlyValidItemUoms()
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext();
        await using var dbContext = CreateDbContext(executionContext);
        await TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext);
        var references = await SeedReferencesAsync(dbContext);
        var service = new PricingService(dbContext, executionContext);

        var options = await service.GetItemUomOptionsAsync(references.Item.Id, CancellationToken.None);

        Assert.NotNull(options);
        Assert.Contains(options!.Uoms, option => option.Id == references.Piece.Id);
        Assert.Contains(options.Uoms, option => option.Id == references.Box.Id);
        Assert.DoesNotContain(options.Uoms, option => option.Id == references.InvalidUom.Id);
    }

    private static AppDbContext CreateDbContext(TestRequestExecutionContext executionContext)
    {
        var databasePath = CreateDatabasePath();
        return CreateDbContext(databasePath, executionContext);
    }

    private static AppDbContext CreateDbContext(string databasePath, TestRequestExecutionContext executionContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(SqliteTestDatabase.CreateConnectionString(databasePath))
            .Options;
        var dbContext = new AppDbContext(options, executionContext);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"highcool-pricing-tests-{Guid.NewGuid():N}.db");
    }

    private static async Task<Guid> SeedOrganizationAsync(AppDbContext dbContext, TestRequestExecutionContext executionContext, string name)
    {
        var organization = new Organization
        {
            Name = name,
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            PurchaseOrderPrefix = "PO",
            PurchaseReceiptPrefix = "PR",
            PurchaseReturnPrefix = "RTN",
            PaymentPrefix = "PAY",
            SetupCompleted = true,
            SetupVersion = "v1",
            EnableProcurement = true,
            EnablePurchaseOrders = true,
            EnablePurchaseReceipts = true,
            EnableInventory = true,
            EnableWarehouses = true,
            EnableMultipleWarehouses = true,
            EnableSupplierManagement = true,
            EnableSupplierFinancials = true,
            EnableShortageManagement = true,
            EnableComponentsBom = true,
            EnableUom = true,
            EnableUomConversion = true,
            RequirePoBeforeReceipt = false,
            AllowDirectPurchaseReceipt = true,
            AllowPartialReceipt = true,
            AllowOverReceipt = false,
            OverReceiptTolerancePercent = 0,
            EnablePostingWorkflow = true,
            LockPostedDocuments = true,
            RequireApprovalBeforePosting = false,
            EnableReversals = true,
            RequireReasonForCancelOrReversal = true,
            AllowNegativeStock = false,
            EnableBatchTracking = false,
            EnableSerialTracking = false,
            EnableExpiryTracking = false,
            EnableStockTransfers = true,
            EnableStockAdjustments = true,
            EnablePriceLists = true,
            EnableInventoryCounts = true,
            EnableInventoryIssues = true,
            EnableLowStockAlerts = true,
            CreatedBy = "seed"
        };

        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();
        executionContext.OrganizationId = organization.Id;
        return organization.Id;
    }

    private static async Task RunPricingAttemptAsync(string databasePath, Guid organizationId, Func<PricingService, Task> action)
    {
        try
        {
            await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
            var service = new PricingService(dbContext, TestOrganizationContext.CreateExecutionContext(organizationId));
            await action(service);
        }
        catch (Exception exception) when (
            exception is DuplicateEntityException ||
            exception is ConcurrencyConflictException ||
            exception is InvalidOperationException ||
            exception.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static async Task<PricingReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var piece = new Uom
        {
            Code = "PCS",
            Name = "Piece",
            AllowsFraction = false,
            Precision = 0,
            IsActive = true,
            CreatedBy = "seed"
        };
        var box = new Uom
        {
            Code = "BOX",
            Name = "Box",
            AllowsFraction = false,
            Precision = 0,
            IsActive = true,
            CreatedBy = "seed"
        };
        var invalidUom = new Uom
        {
            Code = "PAL",
            Name = "Pallet",
            AllowsFraction = false,
            Precision = 0,
            IsActive = true,
            CreatedBy = "seed"
        };
        var category = new ItemCategory
        {
            Code = "CAT",
            Name = "Category",
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Uoms.AddRange(piece, box, invalidUom);
        dbContext.ItemCategories.Add(category);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITEM-1",
            Name = "Priced Item",
            BaseUomId = piece.Id,
            CategoryId = category.Id,
            MinimumStockQuantity = 0m,
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Items.Add(item);
        dbContext.UomConversions.Add(new UomConversion
        {
            FromUomId = box.Id,
            ToUomId = piece.Id,
            Factor = 10m,
            RoundingMode = RoundingMode.None,
            IsActive = true,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        return new PricingReferences(piece, box, invalidUom, item);
    }

    private static async Task SetPermissionsAsync(AppDbContext dbContext, params string[] permissions)
    {
        var membership = await dbContext.OrganizationMemberships.IgnoreQueryFilters().SingleAsync();
        membership.IsOwner = false;

        dbContext.RolePermissions.RemoveRange(dbContext.RolePermissions.IgnoreQueryFilters());
        dbContext.MembershipRoles.RemoveRange(dbContext.MembershipRoles.IgnoreQueryFilters());

        var role = new Role
        {
            OrganizationId = membership.OrganizationId,
            Name = "Pricing Test Role",
            TemplateKey = "pricing-test",
            IsProtected = false,
            CreatedBy = "seed"
        };
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        dbContext.MembershipRoles.Add(new MembershipRole
        {
            OrganizationId = membership.OrganizationId,
            MembershipId = membership.Id,
            RoleId = role.Id,
            CreatedBy = "seed"
        });
        dbContext.RolePermissions.AddRange(Permissions.Expand(permissions).Distinct(StringComparer.OrdinalIgnoreCase).Select(permission => new RolePermission
        {
            OrganizationId = membership.OrganizationId,
            RoleId = role.Id,
            PermissionKey = permission,
            CreatedBy = "seed"
        }));
        await dbContext.SaveChangesAsync();
    }

    private sealed record PricingReferences(Uom Piece, Uom Box, Uom InvalidUom, Item Item);

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-pricing-api-tests-{Guid.NewGuid():N}.db");

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
}
