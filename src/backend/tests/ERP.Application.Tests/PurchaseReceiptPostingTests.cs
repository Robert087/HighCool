using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using ERP.Infrastructure.Statements;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class PurchaseReceiptPostingTests
{
    [Fact]
    public async Task PostAsync_ShouldCreateStockLedgerShortageLedgerSupplierStatementAndMarkReceiptPosted()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext);
        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: 38m, shortageReasonId: references.ShortageReason.Id);

        var service = CreatePostingService(dbContext);

        var result = await service.PostAsync(receipt.Id, "tester", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DocumentStatus.Posted, result!.Status);

        var stockEntry = await dbContext.StockLedgerEntries.SingleAsync();
        Assert.Equal(receipt.Id, stockEntry.SourceDocId);
        Assert.Equal(receipt.Lines.Single().Id, stockEntry.SourceLineId);
        Assert.Equal(2m, stockEntry.QtyIn);
        Assert.Equal(20m, stockEntry.BaseQty);
        Assert.Equal(20m, stockEntry.RunningBalanceQty);
        Assert.Equal(StockTransactionType.PurchaseReceipt, stockEntry.TransactionType);

        var shortageEntry = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(40m, shortageEntry.ExpectedQty);
        Assert.Equal(38m, shortageEntry.ActualQty);
        Assert.Equal(2m, shortageEntry.ShortageQty);
        Assert.True(shortageEntry.AffectsSupplierBalance);
        Assert.Equal(ShortageEntryStatus.Open, shortageEntry.Status);

        var statementEntry = await dbContext.SupplierStatementEntries.SingleAsync();
        Assert.Equal(receipt.SupplierId, statementEntry.SupplierId);
        Assert.Equal(Domain.Statements.SupplierStatementEffectType.PurchaseReceipt, statementEntry.EffectType);
        Assert.Equal(Domain.Statements.SupplierStatementSourceDocumentType.PurchaseReceipt, statementEntry.SourceDocType);
        Assert.Equal(0m, statementEntry.Debit);
        Assert.Equal(0m, statementEntry.Credit);
    }

    [Fact]
    public async Task PostAsync_ShouldBeIdempotentWhenCalledTwice()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext);
        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: 38m, shortageReasonId: references.ShortageReason.Id);

        var service = CreatePostingService(dbContext);

        var firstResult = await service.PostAsync(receipt.Id, "tester", CancellationToken.None);
        var secondResult = await service.PostAsync(receipt.Id, "tester", CancellationToken.None);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(DocumentStatus.Posted, secondResult!.Status);
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync());
        Assert.Equal(1, await dbContext.ShortageLedgerEntries.CountAsync());
        Assert.Equal(1, await dbContext.SupplierStatementEntries.CountAsync());
    }

    [Fact]
    public async Task PostAsync_ShouldRejectMissingActualComponentRowsForBomItems()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext);
        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: null, shortageReasonId: null);

        var service = CreatePostingService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PostAsync(receipt.Id, "tester", CancellationToken.None));

        Assert.Contains("missing one or more auto-filled component rows", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsync_ShouldAllowPositiveShortageWithoutReason()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext);
        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: 38m, shortageReasonId: null);

        var service = CreatePostingService(dbContext);

        var result = await service.PostAsync(receipt.Id, "tester", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DocumentStatus.Posted, result!.Status);

        var shortageEntry = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(2m, shortageEntry.ShortageQty);
        Assert.Null(shortageEntry.ShortageReasonCodeId);
        Assert.False(shortageEntry.AffectsSupplierBalance);
        Assert.Equal("NotRequired", shortageEntry.ApprovalStatus);
    }

    [Fact]
    public async Task PostAsync_ShouldAllowPostingWhenNoShortageExistsWithoutReason()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext);
        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: 40m, shortageReasonId: null);

        var service = CreatePostingService(dbContext);

        var result = await service.PostAsync(receipt.Id, "tester", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DocumentStatus.Posted, result!.Status);
        Assert.Equal(0, await dbContext.ShortageLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task PostAsync_ShouldRejectOutdatedExpectedComponentQty()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext);
        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: 40m, shortageReasonId: null);
        receipt.Lines.Single().Components.Single().ExpectedQty = 39m;
        await dbContext.SaveChangesAsync();

        var service = CreatePostingService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PostAsync(receipt.Id, "tester", CancellationToken.None));

        Assert.Contains("Expected quantity for component", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsync_ShouldCarryForwardRunningBalance()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext);
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.ParentItem.Id,
            WarehouseId = references.Warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = 1m,
            QtyOut = 0m,
            UomId = references.BoxUom.Id,
            BaseQty = 10m,
            RunningBalanceQty = 10m,
            TransactionDate = DateTime.UtcNow.Date.AddDays(-1),
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: 40m, shortageReasonId: null);
        var service = CreatePostingService(dbContext);

        await service.PostAsync(receipt.Id, "tester", CancellationToken.None);

        var latestEntry = await dbContext.StockLedgerEntries
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstAsync();

        Assert.Equal(30m, latestEntry.RunningBalanceQty);
    }

    [Fact]
    public async Task PostAsync_ShouldShowContextWhenReceiptLineUomConversionIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedPostingReferencesAsync(dbContext, includeReceiptConversion: false);
        var receipt = await CreateDraftReceiptAsync(dbContext, references, actualComponentQty: 40m, shortageReasonId: null);

        var service = CreatePostingService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PostAsync(receipt.Id, "tester", CancellationToken.None));

        Assert.Contains("Purchase receipt line 1", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(references.ParentItem.Code, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("global UOM conversion", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IPurchaseReceiptPostingService CreatePostingService(AppDbContext dbContext)
    {
        var quantityConversionService = new QuantityConversionService(dbContext);
        var receiptService = new PurchaseReceiptService(dbContext, quantityConversionService);
        var stockLedgerService = new StockLedgerService(dbContext, quantityConversionService);
        var shortageDetectionService = new ShortageDetectionService(dbContext, quantityConversionService);

        return new PurchaseReceiptPostingService(
            dbContext,
            receiptService,
            stockLedgerService,
            shortageDetectionService,
            quantityConversionService,
            new SupplierStatementPostingService(dbContext));
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-posting-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<PostingReferences> SeedPostingReferencesAsync(
        AppDbContext dbContext,
        bool includeReceiptConversion = true)
    {
        var supplier = new Supplier
        {
            Code = "SUP-POST",
            Name = "Posting Supplier",
            StatementName = "Posting Supplier",
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

        var boxUom = new Uom
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

        if (includeReceiptConversion)
        {
            dbContext.UomConversions.Add(new UomConversion
            {
                FromUomId = boxUom.Id,
                ToUomId = pieceUom.Id,
                Factor = 10m,
                RoundingMode = RoundingMode.None,
                IsActive = true,
                CreatedBy = "seed"
            });
        }

        var componentItem = new Item
        {
            Code = "CMP-POST",
            Name = "Component",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        var parentItem = new Item
        {
            Code = "ITM-POST",
            Name = "Parent Item",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(componentItem, parentItem);
        await dbContext.SaveChangesAsync();

        dbContext.ItemComponents.Add(new ItemComponent
        {
            ItemId = parentItem.Id,
            ComponentItemId = componentItem.Id,
            UomId = pieceUom.Id,
            Quantity = 2m,
            CreatedBy = "seed"
        });

        var shortageReason = new ShortageReasonCode
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

        return new PostingReferences(supplier, warehouse, pieceUom, boxUom, parentItem, componentItem, shortageReason);
    }

    private static async Task<PurchaseReceipt> CreateDraftReceiptAsync(
        AppDbContext dbContext,
        PostingReferences references,
        decimal? actualComponentQty,
        Guid? shortageReasonId)
    {
        var receipt = new PurchaseReceipt
        {
            ReceiptNo = $"PRD-POST-{Guid.NewGuid():N}".Substring(0, 18),
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            Status = DocumentStatus.Draft,
            CreatedBy = "seed",
            Lines =
            [
                new PurchaseReceiptLine
                {
                    LineNo = 1,
                    ItemId = references.ParentItem.Id,
                    OrderedQtySnapshot = 2m,
                    ReceivedQty = 2m,
                    UomId = references.BoxUom.Id,
                    CreatedBy = "seed",
                    Components = actualComponentQty.HasValue
                        ? [
                            new PurchaseReceiptLineComponent
                            {
                                ComponentItemId = references.ComponentItem.Id,
                                ExpectedQty = 40m,
                                ActualReceivedQty = actualComponentQty.Value,
                                UomId = references.PieceUom.Id,
                                ShortageReasonCodeId = shortageReasonId,
                                CreatedBy = "seed"
                            }
                        ]
                        : []
                }
            ]
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();
        return receipt;
    }

    private sealed record PostingReferences(
        Supplier Supplier,
        Warehouse Warehouse,
        Uom PieceUom,
        Uom BoxUom,
        Item ParentItem,
        Item ComponentItem,
        ShortageReasonCode ShortageReason);
}
