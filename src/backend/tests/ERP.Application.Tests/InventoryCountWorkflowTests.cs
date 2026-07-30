using ERP.Application.Inventory.Counts;
using ERP.Application.Common.Pagination;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Common.Numbering;
using ERP.Infrastructure.Inventory;
using ERP.Infrastructure.Inventory.Counts;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class InventoryCountWorkflowTests
{
    [Fact]
    public async Task CreateUpdateAndDeleteDraft_ShouldNumberSequentiallyAndRejectDuplicateItems()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);

        var first = await service.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 3m)]), "tester", CancellationToken.None);
        var second = await service.CreateDraftAsync(Request(references, [Line(1, references.SecondItem.Id, references.PieceUom.Id, 2m)]), "tester", CancellationToken.None);
        var updated = await service.UpdateDraftAsync(first.Id, Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 4m)]), "tester", CancellationToken.None);

        Assert.Equal("CNT-000001", first.CountNo);
        Assert.Equal("CNT-000002", second.CountNo);
        Assert.NotNull(updated);
        Assert.Equal("CNT-000001", updated!.CountNo);

        var duplicateException = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            new UpsertInventoryCountRequestValidator().ValidateAndThrowAsync(
                Request(references, [
                    Line(1, references.Item.Id, references.PieceUom.Id, 1m),
                    Line(2, references.Item.Id, references.PieceUom.Id, 2m)
                ])));
        Assert.Contains("same item", duplicateException.Message, StringComparison.OrdinalIgnoreCase);

        Assert.True(await service.DeleteDraftAsync(first.Id, CancellationToken.None));
        Assert.Equal(1, await dbContext.InventoryCounts.CountAsync());
    }

    [Fact]
    public async Task Post_ShouldUseLatestLedgerStockAndCreateOnlyVarianceRows()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
        await SeedStockInAsync(dbContext, references, references.SecondItem.Id, 6m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(references, [
                Line(1, references.Item.Id, references.PieceUom.Id, 13m),
                Line(2, references.SecondItem.Id, references.PieceUom.Id, 3m),
                Line(3, references.ThirdItem.Id, references.PieceUom.Id, 0m)
            ]),
            "tester",
            CancellationToken.None);

        await SeedStockInAsync(dbContext, references, references.Item.Id, 2m);
        await SeedStockInAsync(dbContext, references, references.ThirdItem.Id, 5m);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        var lines = posted.Lines.OrderBy(line => line.LineNo).ToArray();
        Assert.Equal(12m, lines[0].BaseSystemQty);
        Assert.Equal(13m, lines[0].BaseCountedQty);
        Assert.Equal(1m, lines[0].BaseVarianceQty);
        Assert.Equal(6m, lines[1].BaseSystemQty);
        Assert.Equal(-3m, lines[1].BaseVarianceQty);
        Assert.Equal(5m, lines[2].BaseSystemQty);
        Assert.Equal(-5m, lines[2].BaseVarianceQty);

        var countEntries = await dbContext.StockLedgerEntries
            .Where(entry => entry.SourceDocId == created.Id)
            .OrderBy(entry => entry.TransactionType)
            .ToListAsync();
        Assert.Equal(3, countEntries.Count);
        Assert.Contains(countEntries, entry => entry.TransactionType == StockTransactionType.InventoryCountIncrease && entry.BaseQty == 1m && entry.QtyIn == 1m);
        Assert.Equal(2, countEntries.Count(entry => entry.TransactionType == StockTransactionType.InventoryCountDecrease));
        Assert.Equal(13m, await LatestBalanceAsync(dbContext, references.Item.Id, references.Warehouse.Id));
        Assert.Equal(3m, await LatestBalanceAsync(dbContext, references.SecondItem.Id, references.Warehouse.Id));
        Assert.Equal(0m, await LatestBalanceAsync(dbContext, references.ThirdItem.Id, references.Warehouse.Id));
    }

    [Fact]
    public async Task Refresh_ShouldUpdateDraftSystemQuantitiesAndPreserveCountedQuantityWithoutLedgerRows()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
        var documentService = CreateDocumentService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 12m)]), "tester", CancellationToken.None);

        await SeedStockInAsync(dbContext, references, references.Item.Id, 5m);

        var refreshed = await documentService.RefreshSystemQuantitiesAsync(created.Id, "refresher", CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.Equal(DocumentStatus.Draft, refreshed!.Status);
        Assert.Equal(12m, refreshed.Lines.Single().CountedQty);
        Assert.Equal(15m, refreshed.Lines.Single().SystemQty);
        Assert.Equal(15m, refreshed.Lines.Single().BaseSystemQty);
        Assert.Equal(-3m, refreshed.Lines.Single().VarianceQty);
        Assert.Equal(-3m, refreshed.Lines.Single().BaseVarianceQty);
        Assert.Equal(0, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));

        await SeedStockInAsync(dbContext, references, references.SecondItem.Id, 1m);
        var posted = await documentService.CreateDraftAsync(Request(references, [Line(1, references.SecondItem.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        await CreatePostingService(dbContext).PostAsync(posted.Id, "poster", CancellationToken.None);

        var postedRefreshError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documentService.RefreshSystemQuantitiesAsync(posted.Id, "refresher", CancellationToken.None));
        Assert.Contains("Draft", postedRefreshError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ShouldUseServerSideUomConversionForAlternateUom()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 20m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.BoxUom.Id, 3m)]), "tester", CancellationToken.None);

        await SeedStockInAsync(dbContext, references, references.Item.Id, 5m);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        var line = posted!.Lines.Single();
        Assert.Equal(2.5m, line.SystemQty);
        Assert.Equal(3m, line.CountedQty);
        Assert.Equal(0.5m, line.VarianceQty);
        Assert.Equal(25m, line.BaseSystemQty);
        Assert.Equal(30m, line.BaseCountedQty);
        Assert.Equal(5m, line.BaseVarianceQty);

        var entry = await dbContext.StockLedgerEntries.SingleAsync(entity => entity.SourceDocId == created.Id);
        Assert.Equal(StockTransactionType.InventoryCountIncrease, entry.TransactionType);
        Assert.Equal(references.BoxUom.Id, entry.UomId);
        Assert.Equal(0.5m, entry.QtyIn);
        Assert.Equal(5m, entry.BaseQty);
    }

    [Fact]
    public async Task Post_ShouldCreateNoLedgerRowsForZeroVarianceAndBeIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 5m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 5m)]), "tester", CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        var secondPost = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.NotNull(secondPost);
        Assert.Equal(DocumentStatus.Posted, secondPost!.Status);
        Assert.Equal(0, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));
        Assert.Equal(5m, posted!.Lines.Single().BaseSystemQty);
        Assert.Equal(0m, posted.Lines.Single().BaseVarianceQty);
    }

    [Fact]
    public async Task Post_ShouldAllowCountToResetNegativeStockWithoutOrdinaryStockOutBlocking()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockOutAsync(dbContext, references, references.Item.Id, 2m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 0m)]), "tester", CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        var line = posted!.Lines.Single();
        Assert.Equal(-2m, line.BaseSystemQty);
        Assert.Equal(0m, line.BaseCountedQty);
        Assert.Equal(2m, line.BaseVarianceQty);
        Assert.Equal(StockTransactionType.InventoryCountIncrease, await dbContext.StockLedgerEntries.Where(entry => entry.SourceDocId == created.Id).Select(entry => entry.TransactionType).SingleAsync());
        Assert.Equal(0m, await LatestBalanceAsync(dbContext, references.Item.Id, references.Warehouse.Id));
    }

    [Fact]
    public async Task Cancel_ShouldReverseOnlyPostedVarianceRowsAndBeIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
        await SeedStockInAsync(dbContext, references, references.SecondItem.Id, 10m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(references, [
                Line(1, references.Item.Id, references.PieceUom.Id, 12m),
                Line(2, references.SecondItem.Id, references.PieceUom.Id, 7m)
            ]),
            "tester",
            CancellationToken.None);
        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        var canceled = await postingService.CancelAsync(created.Id, "canceler", CancellationToken.None);
        var secondCancel = await postingService.CancelAsync(created.Id, "canceler", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.NotNull(secondCancel);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        Assert.Equal(4, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));
        Assert.Contains(await dbContext.StockLedgerEntries.Where(entry => entry.SourceDocId == created.Id).ToListAsync(), entry => entry.TransactionType == StockTransactionType.InventoryCountCancellationOut && entry.BaseQty == 2m);
        Assert.Contains(await dbContext.StockLedgerEntries.Where(entry => entry.SourceDocId == created.Id).ToListAsync(), entry => entry.TransactionType == StockTransactionType.InventoryCountCancellationIn && entry.BaseQty == 3m);
    }

    [Fact]
    public async Task Cancel_ShouldBlockWhenReversingIncreaseWouldMakeStockNegativeUnlessOrganizationAllowsIt()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 15m)]), "tester", CancellationToken.None);
        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        await SeedStockOutAsync(dbContext, references, references.Item.Id, 13m);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.CancelAsync(created.Id, "canceler", CancellationToken.None));
        Assert.Contains("Insufficient stock", blocked.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Posted, await dbContext.InventoryCounts.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());

        await SetAllowNegativeStockAsync(dbContext, true);
        var canceled = await postingService.CancelAsync(created.Id, "canceler", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
    }

    [Fact]
    public async Task List_ShouldFilterSortAndPageOnServer()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var first = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        var second = await documentService.CreateDraftAsync(Request(references, [Line(1, references.SecondItem.Id, references.PieceUom.Id, 2m)]), "tester", CancellationToken.None);
        var third = await documentService.CreateDraftAsync(Request(references, [Line(1, references.ThirdItem.Id, references.PieceUom.Id, 3m)]), "tester", CancellationToken.None);

        var firstEntity = await dbContext.InventoryCounts.SingleAsync(entity => entity.Id == first.Id);
        var secondEntity = await dbContext.InventoryCounts.SingleAsync(entity => entity.Id == second.Id);
        var thirdEntity = await dbContext.InventoryCounts.SingleAsync(entity => entity.Id == third.Id);
        firstEntity.CountDate = DateTime.UtcNow.Date.AddDays(-2);
        secondEntity.CountDate = DateTime.UtcNow.Date.AddDays(-1);
        thirdEntity.CountDate = DateTime.UtcNow.Date;
        await dbContext.SaveChangesAsync();

        var page = await documentService.ListAsync(
            new InventoryCountListQuery(
                Search: "Count Warehouse",
                CountNo: "CNT-",
                WarehouseId: references.Warehouse.Id,
                Status: DocumentStatus.Draft,
                FromDate: DateTime.UtcNow.Date.AddDays(-2),
                ToDate: DateTime.UtcNow.Date,
                Page: 2,
                PageSize: 1,
                SortBy: "countDate",
                SortDirection: SortDirection.Asc),
            CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Page);
        Assert.Equal(second.Id, page.Items.Single().Id);
    }

    [Fact]
    public async Task PostConcurrently_ShouldCreateOneSetOfLedgerRows()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-count-concurrent-post-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid countId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var initContext))
            {
                organizationId = initContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                await SeedStockInAsync(dbContext, references, references.Item.Id, 5m);
                var documentService = CreateDocumentService(dbContext);
                var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 9m)]), "tester", CancellationToken.None);
                countId = created.Id;
            }

            var tasks = Enumerable.Range(0, 2)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    var postingService = CreatePostingService(dbContext);
                    return await postingService.PostAsync(countId, "poster", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));

            Assert.All(results, result => Assert.NotNull(result));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == countId));
            Assert.Equal(DocumentStatus.Posted, await verifyContext.InventoryCounts.Where(entity => entity.Id == countId).Select(entity => entity.Status).SingleAsync());
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task CancelConcurrently_ShouldCreateOneSetOfReversalRows()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-count-concurrent-cancel-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid countId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var initContext))
            {
                organizationId = initContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                await SeedStockInAsync(dbContext, references, references.Item.Id, 5m);
                var documentService = CreateDocumentService(dbContext);
                var postingService = CreatePostingService(dbContext);
                var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 7m)]), "tester", CancellationToken.None);
                await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
                countId = created.Id;
            }

            var tasks = Enumerable.Range(0, 2)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    var postingService = CreatePostingService(dbContext);
                    return await postingService.CancelAsync(countId, "canceler", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));

            Assert.All(results, result => Assert.NotNull(result));
            Assert.Equal(DocumentStatus.Canceled, await verifyContext.InventoryCounts.Where(entity => entity.Id == countId).Select(entity => entity.Status).SingleAsync());
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == countId && entry.TransactionType == StockTransactionType.InventoryCountIncrease));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == countId && entry.TransactionType == StockTransactionType.InventoryCountCancellationOut));
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-count-tests-{Guid.NewGuid():N}.db");
        return CreateInitializedDbContext(databasePath, out _);
    }

    private static AppDbContext CreateInitializedDbContext(
        string databasePath,
        out TestRequestExecutionContext executionContext)
    {
        executionContext = TestOrganizationContext.CreateExecutionContext();
        var dbContext = CreateDbContext(databasePath, executionContext);
        dbContext.Database.EnsureCreated();
        TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext).GetAwaiter().GetResult();
        return dbContext;
    }

    private static AppDbContext CreateDbContext(string databasePath, TestRequestExecutionContext executionContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(SqliteTestDatabase.CreateConnectionString(databasePath))
            .Options;

        return new AppDbContext(options, executionContext);
    }

    private static IInventoryCountService CreateDocumentService(AppDbContext dbContext)
    {
        var organizationId = dbContext.Organizations
            .Select(entity => entity.Id)
            .Single();
        return CreateDocumentService(dbContext, organizationId);
    }

    private static IInventoryCountService CreateDocumentService(AppDbContext dbContext, Guid organizationId)
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext(organizationId);

        return new InventoryCountService(
            dbContext,
            new QuantityConversionService(dbContext),
            new DocumentNumberService(dbContext, executionContext));
    }

    private static IInventoryCountPostingService CreatePostingService(AppDbContext dbContext)
    {
        return new InventoryCountPostingService(
            dbContext,
            CreateDocumentService(dbContext),
            new QuantityConversionService(dbContext),
            new StockAvailabilityService(dbContext));
    }

    private static async Task<CountReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var pieceUom = new Uom
        {
            Code = $"PCS-{Guid.NewGuid():N}"[..12],
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };
        var boxUom = new Uom
        {
            Code = $"BOX-{Guid.NewGuid():N}"[..12],
            Name = "Boxes",
            Precision = 6,
            AllowsFraction = true,
            IsActive = true,
            CreatedBy = "seed"
        };
        var warehouse = new Warehouse
        {
            Code = $"CNT-{Guid.NewGuid():N}"[..12],
            Name = "Count Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };
        dbContext.Uoms.AddRange(pieceUom, boxUom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        dbContext.UomConversions.AddRange(
            new UomConversion
            {
                FromUomId = boxUom.Id,
                ToUomId = pieceUom.Id,
                Factor = 10m,
                RoundingMode = RoundingMode.None,
                IsActive = true,
                CreatedBy = "seed"
            },
            new UomConversion
            {
                FromUomId = pieceUom.Id,
                ToUomId = boxUom.Id,
                Factor = 0.1m,
                RoundingMode = RoundingMode.None,
                IsActive = true,
                CreatedBy = "seed"
            });
        await dbContext.SaveChangesAsync();

        var items = Enumerable.Range(1, 3)
            .Select(index => new Item
            {
                Code = $"CNT-IT{index}-{Guid.NewGuid():N}"[..12],
                Name = $"Count Item {index}",
                BaseUomId = pieceUom.Id,
                IsActive = true,
                IsSellable = true,
                HasComponents = false,
                CreatedBy = "seed"
            })
            .ToArray();
        dbContext.Items.AddRange(items);
        await dbContext.SaveChangesAsync();

        return new CountReferences(warehouse, pieceUom, boxUom, items[0], items[1], items[2]);
    }

    private static async Task SeedStockInAsync(AppDbContext dbContext, CountReferences references, Guid itemId, decimal baseQty)
    {
        var currentBalance = await CurrentBalanceAsync(dbContext, itemId, references.Warehouse.Id);
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = itemId,
            WarehouseId = references.Warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = baseQty,
            QtyOut = 0m,
            UomId = references.PieceUom.Id,
            BaseQty = baseQty,
            RunningBalanceQty = currentBalance + baseQty,
            TransactionDate = DateTime.UtcNow.Date,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStockOutAsync(AppDbContext dbContext, CountReferences references, Guid itemId, decimal baseQty)
    {
        var currentBalance = await CurrentBalanceAsync(dbContext, itemId, references.Warehouse.Id);
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = itemId,
            WarehouseId = references.Warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceiptReversal,
            SourceDocType = SourceDocumentType.PurchaseReceiptReversal,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = 0m,
            QtyOut = baseQty,
            UomId = references.PieceUom.Id,
            BaseQty = baseQty,
            RunningBalanceQty = currentBalance - baseQty,
            TransactionDate = DateTime.UtcNow.Date,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SetAllowNegativeStockAsync(AppDbContext dbContext, bool allowNegativeStock)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        organization.AllowNegativeStock = allowNegativeStock;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<decimal> CurrentBalanceAsync(AppDbContext dbContext, Guid itemId, Guid warehouseId)
    {
        var balance = await dbContext.StockLedgerEntries
            .Where(entry => entry.ItemId == itemId && entry.WarehouseId == warehouseId)
            .SumAsync(entry => entry.QtyIn > 0m ? (double)entry.BaseQty : -(double)entry.BaseQty);
        return decimal.Round((decimal)balance, 6, MidpointRounding.AwayFromZero);
    }

    private static async Task<decimal> LatestBalanceAsync(AppDbContext dbContext, Guid itemId, Guid warehouseId)
    {
        var balance = await dbContext.StockLedgerEntries
            .Where(entry => entry.ItemId == itemId && entry.WarehouseId == warehouseId)
            .SumAsync(entry => entry.QtyIn > 0m ? (double)entry.BaseQty : -(double)entry.BaseQty);
        return decimal.Round((decimal)balance, 6, MidpointRounding.AwayFromZero);
    }

    private static UpsertInventoryCountRequest Request(
        CountReferences references,
        IReadOnlyList<UpsertInventoryCountLineRequest> lines)
    {
        return new UpsertInventoryCountRequest(
            null,
            DateTime.UtcNow.Date,
            references.Warehouse.Id,
            "Physical count",
            lines);
    }

    private static UpsertInventoryCountLineRequest Line(int lineNo, Guid itemId, Guid uomId, decimal countedQty)
    {
        return new UpsertInventoryCountLineRequest(lineNo, itemId, uomId, countedQty, null);
    }

    private sealed record CountReferences(
        Warehouse Warehouse,
        Uom PieceUom,
        Uom BoxUom,
        Item Item,
        Item SecondItem,
        Item ThirdItem);
}
