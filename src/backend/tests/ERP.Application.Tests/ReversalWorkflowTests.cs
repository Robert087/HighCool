using ERP.Application.Payments;
using ERP.Application.Common.Exceptions;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Purchasing.PurchaseReturns;
using ERP.Application.Reversals;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Domain.Payments;
using ERP.Domain.Purchasing;
using ERP.Domain.Reversals;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Infrastructure.Payments;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using ERP.Infrastructure.Purchasing.PurchaseReturns;
using ERP.Infrastructure.Reversals;
using ERP.Infrastructure.Statements;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class ReversalWorkflowTests
{
    [Fact]
    public async Task PurchaseReturnPosting_ShouldCreateStockOutAndSupplierStatementDebit()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var postedReceipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-RET-0001", 10m, 100m);

        var quantityConversionService = new QuantityConversionService(dbContext);
        var draftService = new PurchaseReturnService(dbContext, quantityConversionService);
        var postingService = new PurchaseReturnPostingService(
            dbContext,
            draftService,
            new SupplierStatementPostingService(dbContext),
            quantityConversionService);

        var draft = await draftService.CreateDraftAsync(
            new UpsertPurchaseReturnRequest(
                "RTN-0001",
                seed.Supplier.Id,
                postedReceipt.Id,
                DateTime.UtcNow.Date,
                "Damaged batch returned",
                [
                    new UpsertPurchaseReturnLineRequest(
                        1,
                        seed.Item.Id,
                        null,
                        seed.Warehouse.Id,
                        4m,
                        seed.Uom.Id,
                        postedReceipt.Lines.Single().Id)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(draft.Id, "tester", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);

        var stockEntry = await dbContext.StockLedgerEntries
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstAsync();

        Assert.Equal(StockTransactionType.PurchaseReturn, stockEntry.TransactionType);
        Assert.Equal(4m, stockEntry.QtyOut);

        var statementEntry = await dbContext.SupplierStatementEntries
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstAsync();

        Assert.Equal(SupplierStatementEffectType.PurchaseReturn, statementEntry.EffectType);
        Assert.Equal(SupplierStatementSourceDocumentType.PurchaseReturn, statementEntry.SourceDocType);
        Assert.Equal(40m, statementEntry.Debit);
        Assert.Equal(0m, statementEntry.Credit);
    }

    [Fact]
    public async Task PurchaseReturnPosting_ShouldRejectQuantityGreaterThanRemainingReturnable()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var postedReceipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-RET-OVER-0001", 10m, 100m);

        var quantityConversionService = new QuantityConversionService(dbContext);
        var draftService = new PurchaseReturnService(dbContext, quantityConversionService);
        var postingService = new PurchaseReturnPostingService(
            dbContext,
            draftService,
            new SupplierStatementPostingService(dbContext),
            quantityConversionService);

        var firstDraft = await draftService.CreateDraftAsync(
            new UpsertPurchaseReturnRequest(
                "RTN-OVER-0001",
                seed.Supplier.Id,
                postedReceipt.Id,
                DateTime.UtcNow.Date,
                null,
                [
                    new UpsertPurchaseReturnLineRequest(
                        1,
                        seed.Item.Id,
                        null,
                        seed.Warehouse.Id,
                        8m,
                        seed.Uom.Id,
                        postedReceipt.Lines.Single().Id)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(firstDraft.Id, "tester", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            draftService.CreateDraftAsync(
                new UpsertPurchaseReturnRequest(
                    "RTN-OVER-0002",
                    seed.Supplier.Id,
                    postedReceipt.Id,
                    DateTime.UtcNow.Date,
                    null,
                    [
                        new UpsertPurchaseReturnLineRequest(
                            1,
                            seed.Item.Id,
                            null,
                            seed.Warehouse.Id,
                            3m,
                            seed.Uom.Id,
                            postedReceipt.Lines.Single().Id)
                    ]),
                "tester",
                CancellationToken.None));

        Assert.Contains("remaining returnable quantity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PurchaseReturnDraft_ShouldRejectDuplicateReferenceReceiptLines()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var postedReceipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-RET-DUP-0001", 10m, 100m);

        var quantityConversionService = new QuantityConversionService(dbContext);
        var draftService = new PurchaseReturnService(dbContext, quantityConversionService);

        var exception = await Assert.ThrowsAsync<DuplicateEntityException>(() =>
            draftService.CreateDraftAsync(
                new UpsertPurchaseReturnRequest(
                    "RTN-DUP-0001",
                    seed.Supplier.Id,
                    postedReceipt.Id,
                    DateTime.UtcNow.Date,
                    null,
                    [
                        new UpsertPurchaseReturnLineRequest(1, seed.Item.Id, null, seed.Warehouse.Id, 2m, seed.Uom.Id, postedReceipt.Lines.Single().Id),
                        new UpsertPurchaseReturnLineRequest(2, seed.Item.Id, null, seed.Warehouse.Id, 1m, seed.Uom.Id, postedReceipt.Lines.Single().Id)
                    ]),
                "tester",
                CancellationToken.None));

        Assert.Contains("duplicate purchase return receipt references", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PurchaseReturnPosting_ShouldNotCreateSupplierStatementRow_WhenNoFinancialBasisExists()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var postedReceipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-RET-ZERO-0001", 10m, 0m);

        var quantityConversionService = new QuantityConversionService(dbContext);
        var draftService = new PurchaseReturnService(dbContext, quantityConversionService);
        var postingService = new PurchaseReturnPostingService(
            dbContext,
            draftService,
            new SupplierStatementPostingService(dbContext),
            quantityConversionService);

        var draft = await draftService.CreateDraftAsync(
            new UpsertPurchaseReturnRequest(
                "RTN-ZERO-0001",
                seed.Supplier.Id,
                postedReceipt.Id,
                DateTime.UtcNow.Date,
                "Return against receipt with no supplier financial basis",
                [
                    new UpsertPurchaseReturnLineRequest(
                        1,
                        seed.Item.Id,
                        null,
                        seed.Warehouse.Id,
                        4m,
                        seed.Uom.Id,
                        postedReceipt.Lines.Single().Id)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(draft.Id, "tester", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(0, await dbContext.SupplierStatementEntries.CountAsync(entity => entity.SourceDocId == posted!.Id));
    }

    [Fact]
    public async Task ReceiptReversal_ShouldCreateOppositeEffectsAndCancelShortage()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-REV-0001", 10m, 100m);

        dbContext.ShortageLedgerEntries.Add(new ShortageLedgerEntry
        {
            PurchaseReceiptId = receipt.Id,
            PurchaseReceiptLineId = receipt.Lines.Single().Id,
            ItemId = seed.Item.Id,
            ComponentItemId = seed.Item.Id,
            ExpectedQty = 10m,
            ActualQty = 8m,
            ShortageQty = 2m,
            ResolvedPhysicalQty = 0m,
            ResolvedFinancialQtyEquivalent = 0m,
            OpenQty = 2m,
            ResolvedAmount = 0m,
            Status = ShortageEntryStatus.Open,
            ApprovalStatus = "NotRequired",
            AffectsSupplierBalance = false,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = new ReceiptReversalService(dbContext, new SupplierStatementPostingService(dbContext));

        var reversal = await service.ReverseAsync(
            receipt.Id,
            new ReverseDocumentRequest(DateTime.UtcNow.Date, "Wrong supplier delivery"),
            "tester",
            CancellationToken.None);

        Assert.NotNull(reversal);

        var updatedReceipt = await dbContext.PurchaseReceipts.SingleAsync(entity => entity.Id == receipt.Id);
        Assert.NotNull(updatedReceipt.ReversalDocumentId);

        var stockReversal = await dbContext.StockLedgerEntries
            .SingleAsync(entity => entity.TransactionType == StockTransactionType.PurchaseReceiptReversal);
        Assert.Equal(10m, stockReversal.QtyOut);

        var statementReversal = await dbContext.SupplierStatementEntries
            .SingleAsync(entity => entity.EffectType == SupplierStatementEffectType.PurchaseReceiptReversal);
        Assert.Equal(SupplierStatementSourceDocumentType.PurchaseReceiptReversal, statementReversal.SourceDocType);
        Assert.Equal(100m, statementReversal.Debit);

        var shortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(ShortageEntryStatus.Canceled, shortage.Status);
        Assert.Equal(0m, shortage.OpenQty);
    }

    [Fact]
    public async Task PaymentReversal_ShouldRestoreOpenBalance_AndPreventDoubleReversal()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-PAY-REV-0001", 10m, 100m);

        var payment = new Payment
        {
            PaymentNo = "PAY-REV-0001",
            PartyType = PaymentPartyType.Supplier,
            PartyId = seed.Supplier.Id,
            Direction = PaymentDirection.OutboundToParty,
            Amount = 100m,
            PaymentDate = DateTime.UtcNow.Date,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Allocations =
            [
                new PaymentAllocation
                {
                    TargetDocType = PaymentTargetDocumentType.PurchaseReceipt,
                    TargetDocId = receipt.Id,
                    AllocatedAmount = 100m,
                    AllocationOrder = 1,
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.Payments.Add(payment);
        dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
        {
            SupplierId = seed.Supplier.Id,
            EntryDate = payment.PaymentDate,
            SourceDocType = SupplierStatementSourceDocumentType.Payment,
            SourceDocId = payment.Id,
            SourceLineId = payment.Allocations.Single().Id,
            EffectType = SupplierStatementEffectType.Payment,
            Debit = 100m,
            Credit = 0m,
            RunningBalance = 0m,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var openBalanceService = new SupplierOpenBalanceService(new SupplierFinancialTargetStateService(dbContext));
        var paymentReversalService = new PaymentReversalService(dbContext, new SupplierStatementPostingService(dbContext));

        var beforeReverse = await openBalanceService.ListAsync(
            new SupplierOpenBalanceQuery(seed.Supplier.Id, PaymentDirection.OutboundToParty, null, null, null),
            CancellationToken.None);
        Assert.Empty(beforeReverse.Items);

        await paymentReversalService.ReverseAsync(
            payment.Id,
            new ReverseDocumentRequest(DateTime.UtcNow.Date, "Duplicate bank advice"),
            "tester",
            CancellationToken.None);

        var paymentReversalEntry = await dbContext.SupplierStatementEntries
            .SingleAsync(entity => entity.EffectType == SupplierStatementEffectType.PaymentReversal);
        Assert.Equal(SupplierStatementSourceDocumentType.PaymentReversal, paymentReversalEntry.SourceDocType);
        Assert.Equal(0m, paymentReversalEntry.Debit);
        Assert.Equal(100m, paymentReversalEntry.Credit);

        var afterReverse = await openBalanceService.ListAsync(
            new SupplierOpenBalanceQuery(seed.Supplier.Id, PaymentDirection.OutboundToParty, null, null, null),
            CancellationToken.None);

        var restoredOpenBalance = Assert.Single(afterReverse.Items);
        Assert.Equal(100m, restoredOpenBalance.OpenAmount);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paymentReversalService.ReverseAsync(
                payment.Id,
                new ReverseDocumentRequest(DateTime.UtcNow.Date, "Try again"),
                "tester",
                CancellationToken.None));
    }

    [Fact]
    public async Task ShortageResolutionReversal_ShouldRestoreOpenQty_AndCreateStockOut()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-SR-REV-0001", 10m, 100m);

        var shortage = new ShortageLedgerEntry
        {
            PurchaseReceiptId = receipt.Id,
            PurchaseReceiptLineId = receipt.Lines.Single().Id,
            ItemId = seed.Item.Id,
            ComponentItemId = seed.Item.Id,
            ExpectedQty = 5m,
            ActualQty = 0m,
            ShortageQty = 5m,
            ResolvedPhysicalQty = 5m,
            ResolvedFinancialQtyEquivalent = 0m,
            OpenQty = 0m,
            ResolvedAmount = 0m,
            Status = ShortageEntryStatus.Resolved,
            ApprovalStatus = "NotRequired",
            AffectsSupplierBalance = false,
            CreatedBy = "seed"
        };

        var resolution = new ShortageResolution
        {
            ResolutionNo = "SR-REV-0001",
            SupplierId = seed.Supplier.Id,
            ResolutionType = ShortageResolutionType.Physical,
            ResolutionDate = DateTime.UtcNow.Date,
            TotalQty = 5m,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Allocations =
            [
                new ShortageResolutionAllocation
                {
                    ShortageLedgerEntry = shortage,
                    AllocationType = ShortageAllocationType.Physical,
                    AllocatedQty = 5m,
                    SequenceNo = 1,
                    AllocationMethod = "Manual",
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.ShortageLedgerEntries.Add(shortage);
        dbContext.ShortageResolutions.Add(resolution);
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = seed.Item.Id,
            WarehouseId = seed.Warehouse.Id,
            TransactionType = StockTransactionType.ShortagePhysicalResolution,
            SourceDocType = SourceDocumentType.ShortageResolution,
            SourceDocId = resolution.Id,
            SourceLineId = resolution.Allocations.Single().Id,
            QtyIn = 5m,
            QtyOut = 0m,
            UomId = seed.Uom.Id,
            BaseQty = 5m,
            RunningBalanceQty = 15m,
            TransactionDate = resolution.ResolutionDate,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = new ShortageResolutionReversalService(dbContext, new SupplierStatementPostingService(dbContext));

        await service.ReverseAsync(
            resolution.Id,
            new ReverseDocumentRequest(DateTime.UtcNow.Date, "Physical replacement received in error"),
            "tester",
            CancellationToken.None);

        var updatedShortage = await dbContext.ShortageLedgerEntries.SingleAsync();
        Assert.Equal(0m, updatedShortage.ResolvedPhysicalQty);
        Assert.Equal(5m, updatedShortage.OpenQty);
        Assert.Equal(ShortageEntryStatus.Open, updatedShortage.Status);

        var stockReversal = await dbContext.StockLedgerEntries
            .SingleAsync(entity => entity.TransactionType == StockTransactionType.ShortageResolutionReversal);
        Assert.Equal(5m, stockReversal.QtyOut);
    }

    [Fact]
    public async Task FinancialShortageResolutionReversal_ShouldCreateTypedOppositeSupplierStatementEffect()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-SR-FIN-0001", 10m, 100m);

        var shortage = new ShortageLedgerEntry
        {
            PurchaseReceiptId = receipt.Id,
            PurchaseReceiptLineId = receipt.Lines.Single().Id,
            ItemId = seed.Item.Id,
            ComponentItemId = seed.Item.Id,
            ExpectedQty = 5m,
            ActualQty = 0m,
            ShortageQty = 5m,
            ResolvedPhysicalQty = 0m,
            ResolvedFinancialQtyEquivalent = 5m,
            OpenQty = 0m,
            ShortageValue = 40m,
            ResolvedAmount = 40m,
            OpenAmount = 0m,
            Status = ShortageEntryStatus.Resolved,
            ApprovalStatus = "NotRequired",
            AffectsSupplierBalance = true,
            CreatedBy = "seed"
        };

        var resolution = new ShortageResolution
        {
            ResolutionNo = "SR-FIN-REV-0001",
            SupplierId = seed.Supplier.Id,
            ResolutionType = ShortageResolutionType.Financial,
            ResolutionDate = DateTime.UtcNow.Date,
            TotalQty = 5m,
            TotalAmount = 40m,
            Currency = "EGP",
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Allocations =
            [
                new ShortageResolutionAllocation
                {
                    ShortageLedgerEntry = shortage,
                    AllocationType = ShortageAllocationType.Financial,
                    AllocatedQty = 5m,
                    FinancialQtyEquivalent = 5m,
                    AllocatedAmount = 40m,
                    ValuationRate = 8m,
                    SequenceNo = 1,
                    AllocationMethod = "Manual",
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.ShortageLedgerEntries.Add(shortage);
        dbContext.ShortageResolutions.Add(resolution);
        await dbContext.SaveChangesAsync();

        dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
        {
            SupplierId = seed.Supplier.Id,
            EntryDate = resolution.ResolutionDate,
            SourceDocType = SupplierStatementSourceDocumentType.ShortageFinancialResolution,
            SourceDocId = resolution.Id,
            SourceLineId = resolution.Allocations.Single().Id,
            EffectType = SupplierStatementEffectType.ShortageFinancialResolution,
            Debit = 40m,
            Credit = 0m,
            RunningBalance = 60m,
            Currency = "EGP",
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = new ShortageResolutionReversalService(dbContext, new SupplierStatementPostingService(dbContext));

        await service.ReverseAsync(
            resolution.Id,
            new ReverseDocumentRequest(DateTime.UtcNow.Date, "Financial settlement posted in error"),
            "tester",
            CancellationToken.None);

        var statementReversal = await dbContext.SupplierStatementEntries
            .SingleAsync(entity => entity.EffectType == SupplierStatementEffectType.ShortageResolutionReversal);
        Assert.Equal(SupplierStatementSourceDocumentType.ShortageResolutionReversal, statementReversal.SourceDocType);
        Assert.Equal(0m, statementReversal.Debit);
        Assert.Equal(40m, statementReversal.Credit);
        Assert.Equal(100m, statementReversal.RunningBalance);
    }

    [Fact]
    public async Task RunningBalance_ShouldRemainConsistent_AfterReturnAndPaymentReversal()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCoreAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, seed, "PR-BAL-0001", 10m, 100m);

        var quantityConversionService = new QuantityConversionService(dbContext);
        var draftService = new PurchaseReturnService(dbContext, quantityConversionService);
        var postingService = new PurchaseReturnPostingService(
            dbContext,
            draftService,
            new SupplierStatementPostingService(dbContext),
            quantityConversionService);

        var returnDraft = await draftService.CreateDraftAsync(
            new UpsertPurchaseReturnRequest(
                "RTN-BAL-0001",
                seed.Supplier.Id,
                receipt.Id,
                DateTime.UtcNow.Date,
                "Return before settlement reversal",
                [
                    new UpsertPurchaseReturnLineRequest(
                        1,
                        seed.Item.Id,
                        null,
                        seed.Warehouse.Id,
                        4m,
                        seed.Uom.Id,
                        receipt.Lines.Single().Id)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(returnDraft.Id, "tester", CancellationToken.None);

        var payment = new Payment
        {
            PaymentNo = "PAY-BAL-0001",
            PartyType = PaymentPartyType.Supplier,
            PartyId = seed.Supplier.Id,
            Direction = PaymentDirection.OutboundToParty,
            Amount = 60m,
            PaymentDate = DateTime.UtcNow.Date,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Allocations =
            [
                new PaymentAllocation
                {
                    TargetDocType = PaymentTargetDocumentType.PurchaseReceipt,
                    TargetDocId = receipt.Id,
                    AllocatedAmount = 60m,
                    AllocationOrder = 1,
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
        {
            SupplierId = seed.Supplier.Id,
            EntryDate = payment.PaymentDate,
            SourceDocType = SupplierStatementSourceDocumentType.Payment,
            SourceDocId = payment.Id,
            SourceLineId = payment.Allocations.Single().Id,
            EffectType = SupplierStatementEffectType.Payment,
            Debit = 60m,
            Credit = 0m,
            RunningBalance = 0m,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var paymentReversalService = new PaymentReversalService(dbContext, new SupplierStatementPostingService(dbContext));
        await paymentReversalService.ReverseAsync(
            payment.Id,
            new ReverseDocumentRequest(DateTime.UtcNow.Date, "Reverse settlement after return review"),
            "tester",
            CancellationToken.None);

        var entries = await dbContext.SupplierStatementEntries
            .OrderBy(entity => entity.CreatedAt)
            .ThenBy(entity => entity.Id)
            .ToListAsync();

        Assert.Equal(
            [
                SupplierStatementEffectType.PurchaseReceipt,
                SupplierStatementEffectType.PurchaseReturn,
                SupplierStatementEffectType.Payment,
                SupplierStatementEffectType.PaymentReversal
            ],
            entries.Select(entity => entity.EffectType).ToArray());

        Assert.Equal(new decimal[] { 100m, 60m, 0m, 60m }, entries.Select(entity => entity.RunningBalance).ToArray());
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-reversal-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<CoreSeed> SeedCoreAsync(AppDbContext dbContext)
    {
        var supplier = new Supplier
        {
            Code = "SUP-REV",
            Name = "Reversal Supplier",
            StatementName = "Reversal Supplier",
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

        var uom = new Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 2,
            AllowsFraction = true,
            IsActive = true,
            CreatedBy = "seed"
        };

        var item = new Item
        {
            Code = "ITM-REV",
            Name = "Reversal Item",
            BaseUom = uom,
            BaseUomId = uom.Id,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Uoms.Add(uom);
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return new CoreSeed(supplier, warehouse, uom, item);
    }

    private static async Task<PurchaseReceipt> SeedPostedReceiptAsync(
        AppDbContext dbContext,
        CoreSeed seed,
        string receiptNo,
        decimal receivedQty,
        decimal payableAmount)
    {
        var receipt = new PurchaseReceipt
        {
            ReceiptNo = receiptNo,
            SupplierId = seed.Supplier.Id,
            WarehouseId = seed.Warehouse.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            SupplierPayableAmount = payableAmount,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Lines =
            [
                new PurchaseReceiptLine
                {
                    LineNo = 1,
                    ItemId = seed.Item.Id,
                    ReceivedQty = receivedQty,
                    UomId = seed.Uom.Id,
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = seed.Item.Id,
            WarehouseId = seed.Warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = receipt.Id,
            SourceLineId = receipt.Lines.Single().Id,
            QtyIn = receivedQty,
            QtyOut = 0m,
            UomId = seed.Uom.Id,
            BaseQty = receivedQty,
            RunningBalanceQty = receivedQty,
            TransactionDate = receipt.ReceiptDate,
            CreatedBy = "seed"
        });

        if (payableAmount > 0m)
        {
            dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
            {
                SupplierId = seed.Supplier.Id,
                EntryDate = receipt.ReceiptDate,
                SourceDocType = SupplierStatementSourceDocumentType.PurchaseReceipt,
                SourceDocId = receipt.Id,
                SourceLineId = receipt.Id,
                EffectType = SupplierStatementEffectType.PurchaseReceipt,
                Debit = 0m,
                Credit = payableAmount,
                RunningBalance = payableAmount,
                CreatedBy = "seed"
            });
        }

        await dbContext.SaveChangesAsync();
        return receipt;
    }

    private sealed record CoreSeed(Supplier Supplier, Warehouse Warehouse, Uom Uom, Item Item);
}
