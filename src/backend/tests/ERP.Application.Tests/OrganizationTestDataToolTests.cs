using ERP.Application.LocalData;
using ERP.Application.Payments;
using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Purchasing.PurchaseReturns;
using ERP.Application.TestData;
using ERP.Domain.Common;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Domain.Statements;
using ERP.Infrastructure.Payments;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseOrders;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using ERP.Infrastructure.Purchasing.PurchaseReturns;
using ERP.Infrastructure.Shortages;
using ERP.Infrastructure.Statements;
using ERP.Infrastructure.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ERP.Application.Tests;

public sealed class OrganizationTestDataToolTests
{
    [Fact]
    public async Task SeedAsync_ShouldPlanWithoutWritingRows_WhenDryRun()
    {
        await using var fixture = await ToolFixture.CreateAsync("Testing");

        var result = await fixture.Service.SeedAsync(
            new SeedOrganizationTestDataRequest(fixture.OrganizationId, "restore-smoke", "small", 7, DryRun: true, Force: false),
            CancellationToken.None);

        Assert.Equal(OrganizationTestDataCommandStatus.Planned, result.Status);
        Assert.Equal(0, await fixture.DbContext.Suppliers.CountAsync());
        Assert.False(File.Exists(result.ManifestPath));
    }

    [Fact]
    public async Task SeedVerifyAndReset_ShouldCreateSnapshotAndRemoveOnlySelectedSeedRun()
    {
        await using var fixture = await ToolFixture.CreateAsync("Testing");
        var otherOrganizationId = await fixture.AddOrganizationAsync("Other Organization");

        var firstSeed = await fixture.Service.SeedAsync(
            new SeedOrganizationTestDataRequest(fixture.OrganizationId, "restore-smoke", "small", 11, DryRun: false, Force: false),
            CancellationToken.None);
        var secondSeed = await fixture.Service.SeedAsync(
            new SeedOrganizationTestDataRequest(otherOrganizationId, "restore-smoke", "small", 11, DryRun: false, Force: false),
            CancellationToken.None);

        Assert.Equal(OrganizationTestDataCommandStatus.Completed, firstSeed.Status);
        Assert.Equal(OrganizationTestDataCommandStatus.Completed, secondSeed.Status);
        Assert.True(File.Exists(firstSeed.ManifestPath));
        Assert.True(File.Exists(firstSeed.SnapshotPath));
        Assert.True(firstSeed.Counts[nameof(Supplier)] > 0);
        Assert.True(firstSeed.Counts[nameof(StockLedgerEntry)] > 0);
        Assert.True(firstSeed.Counts[nameof(SupplierStatementEntry)] > 0);

        var verify = await fixture.Service.VerifyAsync(
            new VerifyOrganizationRestoreRequest(fixture.OrganizationId, firstSeed.SnapshotPath!),
            CancellationToken.None);

        Assert.Equal(OrganizationTestDataCommandStatus.Completed, verify.Status);

        var reset = await fixture.Service.ResetAsync(
            new ResetOrganizationDataRequest(
                fixture.OrganizationId,
                DryRun: false,
                Execute: true,
                Confirmation: $"RESET-ORG-{fixture.OrganizationId}",
                PreserveUsers: true,
                PreserveOrganization: true,
                PreserveSettings: true,
                TestDataOnly: true,
                SeedRunId: firstSeed.RunId,
                SkipSafetyBackup: true),
            CancellationToken.None);

        Assert.Equal(OrganizationTestDataCommandStatus.Completed, reset.Status);
        Assert.Equal(0, await fixture.DbContext.Suppliers.IgnoreQueryFilters().CountAsync(entity => entity.OrganizationId == fixture.OrganizationId));
        Assert.True(await fixture.DbContext.Organizations.IgnoreQueryFilters().AnyAsync(entity => entity.Id == fixture.OrganizationId));
        Assert.True(await fixture.DbContext.Suppliers.IgnoreQueryFilters().AnyAsync(entity => entity.OrganizationId == otherOrganizationId));
        Assert.False(File.Exists(firstSeed.ManifestPath));
        Assert.True(File.Exists(secondSeed.ManifestPath));
    }

    [Fact]
    public async Task SeedAsync_ShouldRejectProductionEnvironment()
    {
        await using var fixture = await ToolFixture.CreateAsync("Production");

        var result = await fixture.Service.SeedAsync(
            new SeedOrganizationTestDataRequest(fixture.OrganizationId, "restore-smoke", "small", 3, DryRun: false, Force: false),
            CancellationToken.None);

        Assert.Equal(OrganizationTestDataCommandStatus.Rejected, result.Status);
        Assert.Contains("blocked", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await fixture.DbContext.Suppliers.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_ShouldFailSafelyWhenRunAlreadyExists()
    {
        await using var fixture = await ToolFixture.CreateAsync("Testing");

        var first = await fixture.Service.SeedAsync(
            new SeedOrganizationTestDataRequest(fixture.OrganizationId, "restore-smoke", "small", 5, DryRun: false, Force: false),
            CancellationToken.None);
        var second = await fixture.Service.SeedAsync(
            new SeedOrganizationTestDataRequest(fixture.OrganizationId, "restore-smoke", "small", 5, DryRun: false, Force: false),
            CancellationToken.None);

        Assert.Equal(OrganizationTestDataCommandStatus.Completed, first.Status);
        Assert.Equal(OrganizationTestDataCommandStatus.Rejected, second.Status);
        Assert.Equal(1, await fixture.DbContext.Suppliers.CountAsync());
    }

    private sealed class ToolFixture : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly string _storageRoot;

        private ToolFixture(
            AppDbContext dbContext,
            OrganizationTestDataService service,
            Guid organizationId,
            string databasePath,
            string storageRoot)
        {
            DbContext = dbContext;
            Service = service;
            OrganizationId = organizationId;
            _databasePath = databasePath;
            _storageRoot = storageRoot;
        }

        public AppDbContext DbContext { get; }

        public OrganizationTestDataService Service { get; }

        public Guid OrganizationId { get; }

        public static async Task<ToolFixture> CreateAsync(string environmentName)
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-test-data-tools-{Guid.NewGuid():N}.db");
            var storageRoot = Path.Combine(Path.GetTempPath(), $"highcool-test-data-tools-storage-{Guid.NewGuid():N}");
            var executionContext = new OrganizationToolExecutionContext();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(SqliteTestDatabase.CreateConnectionString(databasePath))
                .Options;
            var dbContext = new AppDbContext(options, executionContext);
            await dbContext.Database.EnsureCreatedAsync();

            var organization = BuildOrganization("Test Organization");
            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync();
            executionContext.SetOrganization(organization.Id);

            var service = BuildService(dbContext, executionContext, environmentName, storageRoot);
            return new ToolFixture(dbContext, service, organization.Id, databasePath, storageRoot);
        }

        public async Task<Guid> AddOrganizationAsync(string name)
        {
            var organization = BuildOrganization(name);
            DbContext.Organizations.Add(organization);
            await DbContext.SaveChangesAsync();
            return organization.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            TryDelete(_databasePath);
            if (Directory.Exists(_storageRoot))
            {
                SqliteTestDatabase.DeleteDirectoryIfExists(_storageRoot);
            }
        }

        private static OrganizationTestDataService BuildService(
            AppDbContext dbContext,
            OrganizationToolExecutionContext executionContext,
            string environmentName,
            string storageRoot)
        {
            var quantityConversionService = new QuantityConversionService(dbContext);
            var supplierStatementPostingService = new SupplierStatementPostingService(dbContext);

            var purchaseOrderService = new PurchaseOrderService(dbContext);
            var purchaseOrderPostingService = new PurchaseOrderPostingService(dbContext, purchaseOrderService);

            var purchaseReceiptService = new PurchaseReceiptService(dbContext, executionContext, quantityConversionService);
            var purchaseReceiptPostingService = new PurchaseReceiptPostingService(
                dbContext,
                executionContext,
                purchaseReceiptService,
                new StockLedgerService(dbContext, quantityConversionService),
                new ShortageDetectionService(dbContext, quantityConversionService),
                quantityConversionService,
                supplierStatementPostingService);

            var shortageResolutionService = new ShortageResolutionService(dbContext);
            var shortageValidationService = new ShortageResolutionValidationService(dbContext);
            var shortageAllocationService = new ShortageResolutionAllocationService(dbContext);
            var shortageResolutionPostingService = new ShortageResolutionPostingService(
                dbContext,
                shortageResolutionService,
                shortageValidationService,
                shortageAllocationService,
                supplierStatementPostingService);

            var targetStateService = new SupplierFinancialTargetStateService(dbContext);
            var openBalanceService = new SupplierOpenBalanceService(targetStateService);
            var paymentAllocationService = new PaymentAllocationService(openBalanceService);
            var paymentQueryService = new PaymentQueryService(dbContext);
            var paymentService = new PaymentService(dbContext, paymentAllocationService, paymentQueryService);
            var paymentPostingService = new SupplierPaymentPostingService(
                dbContext,
                paymentQueryService,
                paymentAllocationService,
                supplierStatementPostingService);

            var purchaseReturnService = new PurchaseReturnService(dbContext, quantityConversionService);
            var purchaseReturnPostingService = new PurchaseReturnPostingService(
                dbContext,
                purchaseReturnService,
                supplierStatementPostingService,
                quantityConversionService);

            return new OrganizationTestDataService(
                dbContext,
                new TestHostEnvironment(environmentName),
                new TestLocalStoragePathService(storageRoot),
                executionContext,
                purchaseOrderService,
                purchaseOrderPostingService,
                purchaseReceiptService,
                purchaseReceiptPostingService,
                shortageResolutionService,
                shortageResolutionPostingService,
                paymentService,
                paymentPostingService,
                purchaseReturnService,
                purchaseReturnPostingService,
                new SuccessfulBackupService());
        }

        private static Organization BuildOrganization(string name)
        {
            return new Organization
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
                EnablePostingWorkflow = true,
                LockPostedDocuments = true,
                RequireApprovalBeforePosting = false,
                EnableReversals = true,
                RequireReasonForCancelOrReversal = true,
                AllowNegativeStock = false,
                EnableStockTransfers = true,
                EnableStockAdjustments = true,
                CreatedBy = "test"
            };
        }

        private static void TryDelete(string path)
        {
            SqliteTestDatabase.DeleteSqliteFileSet(path);
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "HighCool.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    }

    private sealed class TestLocalStoragePathService : ILocalStoragePathService
    {
        public TestLocalStoragePathService(string root)
        {
            DataDirectory = Path.Combine(root, "Data");
            BackupDirectory = Path.Combine(root, "Backups");
            PendingBackupDirectory = Path.Combine(root, "PendingBackups");
            LogDirectory = Path.Combine(root, "Logs");
        }

        public string DataDirectory { get; }

        public string BackupDirectory { get; }

        public string PendingBackupDirectory { get; }

        public string LogDirectory { get; }

        public string GetSqliteDatabasePath(string fileName) => Path.Combine(DataDirectory, fileName);

        public void EnsureRequiredDirectories()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(PendingBackupDirectory);
            Directory.CreateDirectory(LogDirectory);
        }
    }

    private sealed class SuccessfulBackupService : IDatabaseBackupService
    {
        public Task<BackupResult> CreateBackupAsync(BackupReason reason, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BackupResult(
                BackupStatus.Succeeded,
                Guid.NewGuid().ToString("N"),
                DateTime.UtcNow,
                1,
                "checksum",
                reason,
                "ok"));
        }
    }
}
