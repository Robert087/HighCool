using ERP.Application.Shortages;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Shortages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class ShortageResolutionPostingTests
{
    [Fact]
    public async Task PostAsync_ShouldFullyResolvePhysicalShortage()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(dbContext, references, shortageQty: 10m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Physical,
            [new AllocationSeed(shortage.Id, allocatedQty: 10m)]);

        var service = CreatePostingService(dbContext);

        await service.PostAsync(resolution.Id, "tester", CancellationToken.None);

        var updatedShortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(10m, updatedShortage.ResolvedPhysicalQty);
        Assert.Equal(0m, updatedShortage.ResolvedFinancialQtyEquivalent);
        Assert.Equal(0m, updatedShortage.OpenQty);
        Assert.Equal(ShortageEntryStatus.Resolved, updatedShortage.Status);

        var stockEntry = await dbContext.StockLedgerEntries.SingleAsync();
        Assert.Equal(10m, stockEntry.QtyIn);
        Assert.Equal(StockTransactionType.ShortagePhysicalResolution, stockEntry.TransactionType);
    }

    [Fact]
    public async Task PostAsync_ShouldPartiallyResolvePhysicalShortage()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(dbContext, references, shortageQty: 10m, shortageValue: 100m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Physical,
            [new AllocationSeed(shortage.Id, allocatedQty: 4m)]);

        var service = CreatePostingService(dbContext);

        await service.PostAsync(resolution.Id, "tester", CancellationToken.None);

        var updatedShortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(4m, updatedShortage.ResolvedPhysicalQty);
        Assert.Equal(0m, updatedShortage.ResolvedFinancialQtyEquivalent);
        Assert.Equal(6m, updatedShortage.OpenQty);
        Assert.Equal(60m, updatedShortage.OpenAmount);
        Assert.Equal(ShortageEntryStatus.PartiallyResolved, updatedShortage.Status);
    }

    [Fact]
    public async Task PostAsync_ShouldPartiallyResolveFinancialShortageUsingQuantityEquivalent()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(
            dbContext,
            references,
            shortageQty: 10m,
            affectsSupplierBalance: true,
            shortageValue: 100m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Financial,
            [new AllocationSeed(shortage.Id, allocatedQty: 3m, valuationRate: 10m)],
            resolutionNo: "SR-FIN-0001");

        var service = CreatePostingService(dbContext);

        await service.PostAsync(resolution.Id, "tester", CancellationToken.None);

        var updatedShortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(0m, updatedShortage.ResolvedPhysicalQty);
        Assert.Equal(3m, updatedShortage.ResolvedFinancialQtyEquivalent);
        Assert.Equal(7m, updatedShortage.OpenQty);
        Assert.Equal(30m, updatedShortage.ResolvedAmount);
        Assert.Equal(70m, updatedShortage.OpenAmount);
        Assert.Equal(ShortageEntryStatus.PartiallyResolved, updatedShortage.Status);

        var allocation = await dbContext.ShortageResolutionAllocations.SingleAsync();
        Assert.Equal(ShortageAllocationType.Financial, allocation.AllocationType);
        Assert.Equal(3m, allocation.FinancialQtyEquivalent);

        var statementEntry = await dbContext.SupplierStatementEntries.SingleAsync();
        Assert.Equal(SupplierStatementEffectType.ShortageFinancialResolution, statementEntry.EffectType);
        Assert.Equal(-30m, statementEntry.AmountDelta);
    }

    [Fact]
    public async Task PostAsync_ShouldAllowFinancialResolutionForAnyOpenShortageRow()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(
            dbContext,
            references,
            shortageQty: 8m,
            affectsSupplierBalance: false,
            shortageValue: 80m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Financial,
            [new AllocationSeed(shortage.Id, allocatedQty: 2m, valuationRate: 10m)],
            resolutionNo: "SR-FIN-OPEN-ALL");

        var service = CreatePostingService(dbContext);

        await service.PostAsync(resolution.Id, "tester", CancellationToken.None);

        var updatedShortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(2m, updatedShortage.ResolvedFinancialQtyEquivalent);
        Assert.Equal(6m, updatedShortage.OpenQty);
        Assert.Equal(ShortageEntryStatus.PartiallyResolved, updatedShortage.Status);
        Assert.Equal(1, await dbContext.SupplierStatementEntries.CountAsync());
    }

    [Fact]
    public async Task PostAsync_ShouldSupportMixedSettlementAcrossTime()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(
            dbContext,
            references,
            shortageQty: 10m,
            affectsSupplierBalance: true,
            shortageValue: 100m);

        var physicalResolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Physical,
            [new AllocationSeed(shortage.Id, allocatedQty: 4m)],
            resolutionNo: "SR-MIX-0001");

        var financialResolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Financial,
            [new AllocationSeed(shortage.Id, allocatedQty: 3m, valuationRate: 10m)],
            resolutionNo: "SR-MIX-0002");

        var service = CreatePostingService(dbContext);

        await service.PostAsync(physicalResolution.Id, "tester", CancellationToken.None);
        await service.PostAsync(financialResolution.Id, "tester", CancellationToken.None);

        var updatedShortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(4m, updatedShortage.ResolvedPhysicalQty);
        Assert.Equal(3m, updatedShortage.ResolvedFinancialQtyEquivalent);
        Assert.Equal(3m, updatedShortage.OpenQty);
        Assert.Equal(30m, updatedShortage.ResolvedAmount);
        Assert.Equal(30m, updatedShortage.OpenAmount);
        Assert.Equal(ShortageEntryStatus.PartiallyResolved, updatedShortage.Status);
    }

    [Fact]
    public async Task PostAsync_ShouldResolveRepeatedMixedSettlementOverMultipleDocuments()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(
            dbContext,
            references,
            shortageQty: 10m,
            affectsSupplierBalance: true,
            shortageValue: 100m);

        var firstResolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Physical,
            [new AllocationSeed(shortage.Id, allocatedQty: 4m)],
            resolutionNo: "SR-STEP-1");
        var secondResolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Physical,
            [new AllocationSeed(shortage.Id, allocatedQty: 2m)],
            resolutionNo: "SR-STEP-2");
        var thirdResolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Financial,
            [new AllocationSeed(shortage.Id, allocatedQty: 4m, valuationRate: 10m)],
            resolutionNo: "SR-STEP-3");

        var service = CreatePostingService(dbContext);

        await service.PostAsync(firstResolution.Id, "tester", CancellationToken.None);
        await service.PostAsync(secondResolution.Id, "tester", CancellationToken.None);
        await service.PostAsync(thirdResolution.Id, "tester", CancellationToken.None);

        var updatedShortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(6m, updatedShortage.ResolvedPhysicalQty);
        Assert.Equal(4m, updatedShortage.ResolvedFinancialQtyEquivalent);
        Assert.Equal(0m, updatedShortage.OpenQty);
        Assert.Equal(40m, updatedShortage.ResolvedAmount);
        Assert.Equal(0m, updatedShortage.OpenAmount);
        Assert.Equal(ShortageEntryStatus.Resolved, updatedShortage.Status);

        Assert.Equal(2, await dbContext.StockLedgerEntries.CountAsync());
        Assert.Equal(1, await dbContext.SupplierStatementEntries.CountAsync());
    }

    [Fact]
    public async Task PostAsync_ShouldRejectOverAllocationWhenRemainingOpenQtyWouldBeExceeded()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(
            dbContext,
            references,
            shortageQty: 10m,
            affectsSupplierBalance: true,
            shortageValue: 100m,
            resolvedPhysicalQty: 4m,
            resolvedFinancialQtyEquivalent: 4m,
            resolvedAmount: 40m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Financial,
            [new AllocationSeed(shortage.Id, allocatedQty: 3m, valuationRate: 10m)],
            resolutionNo: "SR-OVER-0001");

        var service = CreatePostingService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PostAsync(resolution.Id, "tester", CancellationToken.None));

        Assert.Contains("open shortage quantity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsync_ShouldAllowPhysicalResolutionWithoutRate()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(dbContext, references, shortageQty: 6m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Physical,
            [new AllocationSeed(shortage.Id, allocatedQty: 2m)],
            resolutionNo: "SR-PHY-NO-RATE");

        var service = CreatePostingService(dbContext);

        await service.PostAsync(resolution.Id, "tester", CancellationToken.None);

        var stockEntry = await dbContext.StockLedgerEntries.SingleAsync();
        Assert.Null(stockEntry.UnitCost);
        Assert.Equal(2m, stockEntry.QtyIn);
    }

    [Fact]
    public async Task PostAsync_ShouldRejectFinancialResolutionWithoutRate()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(dbContext, references, shortageQty: 6m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Financial,
            [new AllocationSeed(shortage.Id, allocatedQty: 2m)],
            resolutionNo: "SR-FIN-NO-RATE");

        var service = CreatePostingService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PostAsync(resolution.Id, "tester", CancellationToken.None));

        Assert.Contains("valuation rate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsync_ShouldBeIdempotentWhenCalledTwice()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        var shortage = await CreateShortageAsync(dbContext, references, shortageQty: 4m);
        var resolution = await CreateResolutionAsync(
            dbContext,
            references.Supplier,
            ShortageResolutionType.Physical,
            [new AllocationSeed(shortage.Id, allocatedQty: 4m)]);

        var service = CreatePostingService(dbContext);

        var first = await service.PostAsync(resolution.Id, "tester", CancellationToken.None);
        var second = await service.PostAsync(resolution.Id, "tester", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(DocumentStatus.Posted, second!.Status);
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync());
        Assert.Equal(0, await dbContext.SupplierStatementEntries.CountAsync());
    }

    [Fact]
    public async Task ListOpenShortagesAsync_ShouldIncludeLegacyRowsWithMissingOpenQty()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedShortageReferencesAsync(dbContext);
        await CreateShortageAsync(dbContext, references, shortageQty: 7m, shortageValue: 70m);

        var legacyRow = await dbContext.ShortageLedgerEntries.SingleAsync();
        legacyRow.OpenQty = 0m;
        legacyRow.OpenAmount = null;
        await dbContext.SaveChangesAsync();

        var service = new ShortageResolutionService(dbContext);

        var result = await service.ListOpenShortagesAsync(
            new OpenShortageQuery(null, references.Supplier.Id, null, null, null, null, null, null),
            CancellationToken.None);

        var shortage = Assert.Single(result);
        Assert.Equal(7m, shortage.OpenQty);
        Assert.Equal(0m, shortage.ResolvedPhysicalQty);
        Assert.Equal(0m, shortage.ResolvedFinancialQtyEquivalent);
        Assert.Equal(ShortageEntryStatus.Open, shortage.Status);
    }

    private static IShortageResolutionPostingService CreatePostingService(AppDbContext dbContext)
    {
        var resolutionService = new ShortageResolutionService(dbContext);
        var validationService = new ShortageResolutionValidationService(dbContext);
        var allocationService = new ShortageResolutionAllocationService(dbContext);

        return new ShortageResolutionPostingService(dbContext, resolutionService, validationService, allocationService);
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-shortage-resolution-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<ShortageReferences> SeedShortageReferencesAsync(AppDbContext dbContext)
    {
        var supplier = new Supplier
        {
            Code = "SUP-SHORT",
            Name = "Shortage Supplier",
            StatementName = "Shortage Supplier",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouse = new Warehouse
        {
            Code = "MAIN",
            Name = "Main Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        var pieceUom = new Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var parentItem = new Item
        {
            Code = "ITM-SHORT",
            Name = "Shortage Parent",
            BaseUomId = Guid.Empty,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = "seed"
        };

        var componentItem = new Item
        {
            Code = "CMP-SHORT",
            Name = "Shortage Component",
            BaseUomId = Guid.Empty,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Uoms.Add(pieceUom);
        await dbContext.SaveChangesAsync();

        parentItem.BaseUomId = pieceUom.Id;
        componentItem.BaseUomId = pieceUom.Id;
        dbContext.Items.AddRange(parentItem, componentItem);
        await dbContext.SaveChangesAsync();

        return new ShortageReferences(supplier, warehouse, pieceUom, parentItem, componentItem);
    }

    private static async Task<ShortageLedgerEntry> CreateShortageAsync(
        AppDbContext dbContext,
        ShortageReferences references,
        decimal shortageQty,
        bool affectsSupplierBalance = false,
        decimal? shortageValue = null,
        decimal resolvedAmount = 0m,
        decimal resolvedPhysicalQty = 0m,
        decimal resolvedFinancialQtyEquivalent = 0m,
        string suffix = "0001")
    {
        var receipt = new PurchaseReceipt
        {
            ReceiptNo = $"PR-SHORT-{suffix}",
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            ReceiptDate = new DateTime(2026, 4, 21),
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var receiptLine = new PurchaseReceiptLine
        {
            PurchaseReceiptId = receipt.Id,
            LineNo = 1,
            ItemId = references.ParentItem.Id,
            ReceivedQty = 1m,
            UomId = references.BaseUom.Id,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceiptLines.Add(receiptLine);
        await dbContext.SaveChangesAsync();

        var resolvedQtyEquivalent = resolvedPhysicalQty + resolvedFinancialQtyEquivalent;
        var valuationRate = shortageValue.HasValue && shortageQty > 0m
            ? shortageValue.Value / shortageQty
            : (decimal?)null;

        var entry = new ShortageLedgerEntry
        {
            PurchaseReceiptId = receipt.Id,
            PurchaseReceiptLineId = receiptLine.Id,
            ItemId = references.ParentItem.Id,
            ComponentItemId = references.ComponentItem.Id,
            ExpectedQty = shortageQty,
            ActualQty = 0m,
            ShortageQty = shortageQty,
            ResolvedPhysicalQty = resolvedPhysicalQty,
            ResolvedFinancialQtyEquivalent = resolvedFinancialQtyEquivalent,
            OpenQty = shortageQty - resolvedQtyEquivalent,
            ShortageValue = shortageValue,
            ResolvedAmount = resolvedAmount,
            OpenAmount = valuationRate.HasValue ? (shortageQty - resolvedQtyEquivalent) * valuationRate.Value : null,
            AffectsSupplierBalance = affectsSupplierBalance,
            ApprovalStatus = "NotRequired",
            Status = resolvedQtyEquivalent == 0m ? ShortageEntryStatus.Open : ShortageEntryStatus.PartiallyResolved,
            CreatedBy = "seed"
        };

        dbContext.ShortageLedgerEntries.Add(entry);
        await dbContext.SaveChangesAsync();
        return entry;
    }

    private static async Task<ShortageResolution> CreateResolutionAsync(
        AppDbContext dbContext,
        Supplier supplier,
        ShortageResolutionType type,
        IReadOnlyList<AllocationSeed> allocations,
        string resolutionNo = "SR-TEST-0001")
    {
        var resolution = new ShortageResolution
        {
            ResolutionNo = resolutionNo,
            SupplierId = supplier.Id,
            ResolutionType = type,
            ResolutionDate = new DateTime(2026, 4, 21),
            TotalQty = type == ShortageResolutionType.Physical ? allocations.Sum(entity => entity.AllocatedQty ?? 0m) : null,
            TotalAmount = type == ShortageResolutionType.Financial
                ? allocations.Sum(entity =>
                    entity.AllocatedQty.HasValue && entity.ValuationRate.HasValue
                        ? entity.AllocatedQty.Value * entity.ValuationRate.Value
                        : 0m)
                : null,
            Currency = "EGP",
            Notes = "Resolution",
            Status = DocumentStatus.Draft,
            CreatedBy = "seed"
        };

        foreach (var allocation in allocations.Select((value, index) => (value, index)))
        {
            resolution.Allocations.Add(new ShortageResolutionAllocation
            {
                ShortageLedgerId = allocation.value.ShortageLedgerId,
                AllocationType = type == ShortageResolutionType.Physical
                    ? ShortageAllocationType.Physical
                    : ShortageAllocationType.Financial,
                AllocatedQty = allocation.value.AllocatedQty,
                AllocatedAmount = type == ShortageResolutionType.Financial &&
                                  allocation.value.AllocatedQty.HasValue &&
                                  allocation.value.ValuationRate.HasValue
                    ? allocation.value.AllocatedQty.Value * allocation.value.ValuationRate.Value
                    : null,
                ValuationRate = allocation.value.ValuationRate,
                AllocationMethod = "Manual",
                SequenceNo = allocation.index + 1,
                CreatedBy = "seed"
            });
        }

        dbContext.ShortageResolutions.Add(resolution);
        await dbContext.SaveChangesAsync();
        return resolution;
    }

    private sealed record ShortageReferences(
        Supplier Supplier,
        Warehouse Warehouse,
        Uom BaseUom,
        Item ParentItem,
        Item ComponentItem);

    private sealed record AllocationSeed(
        Guid ShortageLedgerId,
        decimal? allocatedQty = null,
        decimal? allocatedAmount = null,
        decimal? valuationRate = null)
    {
        public decimal? AllocatedQty { get; init; } = allocatedQty;
        public decimal? AllocatedAmount { get; init; } = allocatedAmount;
        public decimal? ValuationRate { get; init; } = valuationRate;
    }
}
