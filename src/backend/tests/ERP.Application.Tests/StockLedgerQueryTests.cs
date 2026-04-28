using ERP.Application.Inventory;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class StockLedgerQueryTests
{
    [Fact]
    public async Task StockBalanceService_ShouldCalculateLedgerDerivedBalances()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedLedgerEntriesAsync(dbContext, references, Guid.NewGuid());

        var service = new StockBalanceService(dbContext);

        var balances = await service.ListAsync(
            new StockBalanceQuery(null, references.ItemA.Id, references.WarehouseA.Id, null, null, null),
            CancellationToken.None);

        var balance = Assert.Single(balances.Items);
        Assert.Equal(references.ItemA.Id, balance.ItemId);
        Assert.Equal(references.WarehouseA.Id, balance.WarehouseId);
        Assert.Equal("PCS", balance.BaseUomCode);
        Assert.Equal(6m, balance.BalanceQty);
    }

    [Fact]
    public async Task StockBalanceService_ShouldRespectTransactionAndDateFilters()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedLedgerEntriesAsync(dbContext, references, Guid.NewGuid());

        var service = new StockBalanceService(dbContext);
        var balances = await service.ListAsync(
            new StockBalanceQuery(
                null,
                references.ItemA.Id,
                references.WarehouseA.Id,
                StockTransactionType.PurchaseReceipt,
                new DateTime(2026, 4, 20),
                new DateTime(2026, 4, 20, 23, 59, 59)),
            CancellationToken.None);

        var balance = Assert.Single(balances.Items);
        Assert.Equal(10m, balance.BalanceQty);
    }

    [Fact]
    public async Task StockLedgerQueryService_ShouldReturnMovementHistoryWithRunningBalances()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var receipt = new Domain.Purchasing.PurchaseReceipt
        {
            ReceiptNo = "PR-0001",
            SupplierId = references.Supplier.Id,
            WarehouseId = references.WarehouseA.Id,
            ReceiptDate = new DateTime(2026, 4, 20),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };
        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();
        await SeedLedgerEntriesAsync(dbContext, references, receipt.Id);

        var service = new StockLedgerQueryService(dbContext);

        var rows = await service.ListAsync(
            new StockLedgerQuery("Main", references.ItemA.Id, references.WarehouseA.Id, null, null, null),
            CancellationToken.None);

        Assert.Equal(2, rows.TotalCount);
        Assert.Equal(6m, rows.Items[0].RunningBalanceQty);
        Assert.Equal(10m, rows.Items[1].RunningBalanceQty);
        Assert.Equal("PR-0001", rows.Items[1].SourceDocumentNo);
    }

    [Fact]
    public async Task AppDbContext_ShouldRejectStockLedgerUpdatesAndDeletes()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);

        var entry = new StockLedgerEntry
        {
            ItemId = references.ItemA.Id,
            WarehouseId = references.WarehouseA.Id,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = 1m,
            QtyOut = 0m,
            UomId = references.PieceUom.Id,
            BaseQty = 1m,
            RunningBalanceQty = 1m,
            TransactionDate = new DateTime(2026, 4, 20),
            CreatedBy = "seed"
        };

        dbContext.StockLedgerEntries.Add(entry);
        await dbContext.SaveChangesAsync();

        entry.RunningBalanceQty = 2m;
        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
        Assert.Contains("append-only", updateException.Message, StringComparison.OrdinalIgnoreCase);

        dbContext.Entry(entry).State = EntityState.Unchanged;
        dbContext.StockLedgerEntries.Remove(entry);
        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
        Assert.Contains("append-only", deleteException.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-stock-ledger-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<TestReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var pieceUom = new Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var supplier = new Supplier
        {
            Code = "SUP-STOCK",
            Name = "Stock Supplier",
            StatementName = "Stock Supplier",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouseA = new Warehouse
        {
            Code = "MAIN",
            Name = "Main Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouseB = new Warehouse
        {
            Code = "AUX",
            Name = "Aux Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.Add(pieceUom);
        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.AddRange(warehouseA, warehouseB);
        await dbContext.SaveChangesAsync();

        var itemA = new Item
        {
            Code = "ITM-A",
            Name = "Item A",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        var itemB = new Item
        {
            Code = "ITM-B",
            Name = "Item B",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(itemA, itemB);
        await dbContext.SaveChangesAsync();

        return new TestReferences(pieceUom, supplier, warehouseA, warehouseB, itemA, itemB);
    }

    private static async Task SeedLedgerEntriesAsync(AppDbContext dbContext, TestReferences references, Guid receiptId)
    {
        dbContext.StockLedgerEntries.AddRange(
            new StockLedgerEntry
            {
                ItemId = references.ItemA.Id,
                WarehouseId = references.WarehouseA.Id,
                TransactionType = StockTransactionType.PurchaseReceipt,
                SourceDocType = SourceDocumentType.PurchaseReceipt,
                SourceDocId = receiptId,
                SourceLineId = Guid.NewGuid(),
                QtyIn = 10m,
                QtyOut = 0m,
                UomId = references.PieceUom.Id,
                BaseQty = 10m,
                RunningBalanceQty = 10m,
                TransactionDate = new DateTime(2026, 4, 20),
                CreatedBy = "seed"
            },
            new StockLedgerEntry
            {
                ItemId = references.ItemA.Id,
                WarehouseId = references.WarehouseA.Id,
                TransactionType = StockTransactionType.PurchaseReceiptReversal,
                SourceDocType = SourceDocumentType.PurchaseReceiptReversal,
                SourceDocId = Guid.NewGuid(),
                SourceLineId = Guid.NewGuid(),
                QtyIn = 0m,
                QtyOut = 4m,
                UomId = references.PieceUom.Id,
                BaseQty = 4m,
                RunningBalanceQty = 6m,
                TransactionDate = new DateTime(2026, 4, 21),
                CreatedBy = "seed"
            },
            new StockLedgerEntry
            {
                ItemId = references.ItemB.Id,
                WarehouseId = references.WarehouseB.Id,
                TransactionType = StockTransactionType.PurchaseReceipt,
                SourceDocType = SourceDocumentType.PurchaseReceipt,
                SourceDocId = Guid.NewGuid(),
                SourceLineId = Guid.NewGuid(),
                QtyIn = 8m,
                QtyOut = 0m,
                UomId = references.PieceUom.Id,
                BaseQty = 8m,
                RunningBalanceQty = 8m,
                TransactionDate = new DateTime(2026, 4, 19),
                CreatedBy = "seed"
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed record TestReferences(
        Uom PieceUom,
        Supplier Supplier,
        Warehouse WarehouseA,
        Warehouse WarehouseB,
        Item ItemA,
        Item ItemB);
}
