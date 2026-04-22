using ERP.Application.Statements;
using ERP.Domain.MasterData;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Statements;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class SupplierStatementQueryTests
{
    [Fact]
    public async Task ListAsync_ShouldSupportSupplierAndTypeFiltersWithTraceableSourceNumbers()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedAsync(dbContext);

        var queryService = new SupplierStatementQueryService(dbContext);

        var result = await queryService.ListAsync(
            new SupplierStatementQuery(
                null,
                references.SupplierA.Id,
                SupplierStatementEffectType.ShortageFinancialResolution,
                SupplierStatementSourceDocumentType.ShortageResolution,
                new DateTime(2026, 4, 21),
                new DateTime(2026, 4, 21, 23, 59, 59)),
            CancellationToken.None);

        var row = Assert.Single(result);
        Assert.Equal("SR-STMT-0001", row.SourceDocumentNo);
        Assert.Equal(30m, row.Debit);
        Assert.Equal(0m, row.Credit);
        Assert.Equal(-30m, row.RunningBalance);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnOpeningClosingAndCurrentBalance()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedAsync(dbContext);

        var balanceService = new SupplierBalanceService(dbContext);

        var summary = await balanceService.GetSummaryAsync(
            new SupplierStatementSummaryQuery(
                references.SupplierA.Id,
                null,
                null,
                new DateTime(2026, 4, 20),
                new DateTime(2026, 4, 21, 23, 59, 59)),
            CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(references.SupplierA.Id, summary!.SupplierId);
        Assert.Equal(-30m, summary.CurrentBalance);
        Assert.Equal(0m, summary.OpeningBalance);
        Assert.Equal(-30m, summary.ClosingBalance);
        Assert.Equal(30m, summary.TotalDebit);
        Assert.Equal(0m, summary.TotalCredit);
        Assert.Equal(2, summary.EntryCount);
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-supplier-statement-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<StatementReferences> SeedAsync(AppDbContext dbContext)
    {
        var supplierA = new Supplier
        {
            Code = "SUP-STMT-A",
            Name = "Supplier A",
            StatementName = "Supplier A",
            IsActive = true,
            CreatedBy = "seed"
        };

        var supplierB = new Supplier
        {
            Code = "SUP-STMT-B",
            Name = "Supplier B",
            StatementName = "Supplier B",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.AddRange(supplierA, supplierB);
        var warehouseA = new Warehouse
        {
            Code = "WH-STMT-A",
            Name = "Warehouse A",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouseB = new Warehouse
        {
            Code = "WH-STMT-B",
            Name = "Warehouse B",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Warehouses.AddRange(warehouseA, warehouseB);
        await dbContext.SaveChangesAsync();

        var receiptA = new PurchaseReceipt
        {
            ReceiptNo = "PR-STMT-0001",
            SupplierId = supplierA.Id,
            WarehouseId = warehouseA.Id,
            ReceiptDate = new DateTime(2026, 4, 20),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        var receiptB = new PurchaseReceipt
        {
            ReceiptNo = "PR-STMT-0002",
            SupplierId = supplierB.Id,
            WarehouseId = warehouseB.Id,
            ReceiptDate = new DateTime(2026, 4, 22),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        var resolution = new ShortageResolution
        {
            ResolutionNo = "SR-STMT-0001",
            SupplierId = supplierA.Id,
            ResolutionType = Domain.Shortages.ShortageResolutionType.Financial,
            ResolutionDate = new DateTime(2026, 4, 21),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.AddRange(receiptA, receiptB);
        dbContext.ShortageResolutions.Add(resolution);
        await dbContext.SaveChangesAsync();

        dbContext.SupplierStatementEntries.AddRange(
            new SupplierStatementEntry
            {
                SupplierId = supplierA.Id,
                EntryDate = receiptA.ReceiptDate,
                SourceDocType = SupplierStatementSourceDocumentType.PurchaseReceipt,
                SourceDocId = receiptA.Id,
                SourceLineId = receiptA.Id,
                EffectType = SupplierStatementEffectType.PurchaseReceipt,
                Debit = 0m,
                Credit = 0m,
                RunningBalance = 0m,
                Notes = "Receipt amount pending valuation",
                CreatedBy = "seed"
            },
            new SupplierStatementEntry
            {
                SupplierId = supplierA.Id,
                EntryDate = resolution.ResolutionDate,
                SourceDocType = SupplierStatementSourceDocumentType.ShortageResolution,
                SourceDocId = resolution.Id,
                SourceLineId = Guid.NewGuid(),
                EffectType = SupplierStatementEffectType.ShortageFinancialResolution,
                Debit = 30m,
                Credit = 0m,
                RunningBalance = -30m,
                Currency = "EGP",
                Notes = "Financial shortage settlement",
                CreatedBy = "seed"
            },
            new SupplierStatementEntry
            {
                SupplierId = supplierB.Id,
                EntryDate = receiptB.ReceiptDate,
                SourceDocType = SupplierStatementSourceDocumentType.PurchaseReceipt,
                SourceDocId = receiptB.Id,
                SourceLineId = receiptB.Id,
                EffectType = SupplierStatementEffectType.PurchaseReceipt,
                Debit = 0m,
                Credit = 0m,
                RunningBalance = 0m,
                Notes = "Supplier B receipt",
                CreatedBy = "seed"
            });

        await dbContext.SaveChangesAsync();

        return new StatementReferences(supplierA, supplierB);
    }

    private sealed record StatementReferences(Supplier SupplierA, Supplier SupplierB);
}
