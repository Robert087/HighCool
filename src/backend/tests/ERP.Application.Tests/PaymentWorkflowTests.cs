using ERP.Application.Payments;
using ERP.Domain.Common;
using ERP.Domain.MasterData;
using ERP.Domain.Payments;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Infrastructure.Payments;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Statements;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class PaymentWorkflowTests
{
    [Fact]
    public async Task OutboundPayment_ShouldPostAndCreateSupplierStatementEffect()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, references, "PR-PAY-0001", 100m, 100m);
        var services = CreateServices(dbContext);

        var draft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.OutboundToParty,
                100m,
                new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, 100m)),
            "tester",
            CancellationToken.None);

        var posted = await services.PostingService.PostAsync(draft.Id, "tester", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(0m, posted.UnallocatedAmount);

        var statementEntry = await dbContext.SupplierStatementEntries
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstAsync();

        Assert.Equal(SupplierStatementEffectType.Payment, statementEntry.EffectType);
        Assert.Equal(SupplierStatementSourceDocumentType.Payment, statementEntry.SourceDocType);
        Assert.Equal(100m, statementEntry.Debit);
        Assert.Equal(0m, statementEntry.Credit);
        Assert.Equal(0m, statementEntry.RunningBalance);
    }

    [Fact]
    public async Task InboundPayment_ShouldSettleFinancialShortageResolutionAndCreateCreditStatement()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var resolution = await SeedPostedFinancialResolutionAsync(dbContext, references, "SR-PAY-0001", 40m, -40m);
        var services = CreateServices(dbContext);

        var draft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.InboundFromParty,
                40m,
                new AllocationSeed(PaymentTargetDocumentType.ShortageResolution, resolution.Id, 40m)),
            "tester",
            CancellationToken.None);

        var posted = await services.PostingService.PostAsync(draft.Id, "tester", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);

        var statementEntry = await dbContext.SupplierStatementEntries
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstAsync();

        Assert.Equal(0m, statementEntry.Debit);
        Assert.Equal(40m, statementEntry.Credit);
        Assert.Equal(0m, statementEntry.RunningBalance);
    }

    [Fact]
    public async Task InboundOpenBalances_ShouldRemainAvailableAfterPartialPayment()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var resolution = await SeedPostedFinancialResolutionAsync(dbContext, references, "SR-PAY-0002", 100m, -100m);
        var services = CreateServices(dbContext);

        var firstDraft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.InboundFromParty,
                40m,
                new AllocationSeed(PaymentTargetDocumentType.ShortageResolution, resolution.Id, 40m)),
            "tester",
            CancellationToken.None);

        await services.PostingService.PostAsync(firstDraft.Id, "tester", CancellationToken.None);

        var balancesAfterFirstPayment = await services.OpenBalanceService.ListAsync(
            new SupplierOpenBalanceQuery(references.Supplier.Id, PaymentDirection.InboundFromParty, null, null, null),
            CancellationToken.None);

        var openBalance = Assert.Single(balancesAfterFirstPayment);
        Assert.Equal(100m, openBalance.OriginalAmount);
        Assert.Equal(40m, openBalance.AllocatedAmount);
        Assert.Equal(60m, openBalance.OpenAmount);

        var secondDraft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.InboundFromParty,
                60m,
                new AllocationSeed(PaymentTargetDocumentType.ShortageResolution, resolution.Id, 60m)),
            "tester",
            CancellationToken.None);

        await services.PostingService.PostAsync(secondDraft.Id, "tester", CancellationToken.None);

        var balancesAfterSecondPayment = await services.OpenBalanceService.ListAsync(
            new SupplierOpenBalanceQuery(references.Supplier.Id, PaymentDirection.InboundFromParty, null, null, null),
            CancellationToken.None);

        Assert.Empty(balancesAfterSecondPayment);
    }

    [Fact]
    public async Task OpenBalances_ShouldDecreaseAfterPartialAndRepeatedPayments()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, references, "PR-PAY-0002", 100m, 100m);
        var services = CreateServices(dbContext);

        var firstDraft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.OutboundToParty,
                40m,
                new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, 40m)),
            "tester",
            CancellationToken.None);

        await services.PostingService.PostAsync(firstDraft.Id, "tester", CancellationToken.None);

        var balancesAfterFirstPayment = await services.OpenBalanceService.ListAsync(
            new SupplierOpenBalanceQuery(references.Supplier.Id, PaymentDirection.OutboundToParty, null, null, null),
            CancellationToken.None);

        var openBalance = Assert.Single(balancesAfterFirstPayment);
        Assert.Equal(100m, openBalance.OriginalAmount);
        Assert.Equal(40m, openBalance.AllocatedAmount);
        Assert.Equal(60m, openBalance.OpenAmount);

        var secondDraft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.OutboundToParty,
                60m,
                new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, 60m)),
            "tester",
            CancellationToken.None);

        await services.PostingService.PostAsync(secondDraft.Id, "tester", CancellationToken.None);

        var balancesAfterSecondPayment = await services.OpenBalanceService.ListAsync(
            new SupplierOpenBalanceQuery(references.Supplier.Id, PaymentDirection.OutboundToParty, null, null, null),
            CancellationToken.None);

        Assert.Empty(balancesAfterSecondPayment);
    }

    [Fact]
    public async Task Payment_ShouldAllocateAcrossMultipleTargets()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var receiptA = await SeedPostedReceiptAsync(dbContext, references, "PR-PAY-0003", 100m, 100m);
        var receiptB = await SeedPostedReceiptAsync(dbContext, references, "PR-PAY-0004", 50m, 150m);
        var services = CreateServices(dbContext);

        var draft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.OutboundToParty,
                150m,
                new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receiptA.Id, 100m),
                new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receiptB.Id, 50m)),
            "tester",
            CancellationToken.None);

        var posted = await services.PostingService.PostAsync(draft.Id, "tester", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(2, posted!.Allocations.Count);
        Assert.Equal(0m, posted.UnallocatedAmount);
        Assert.Equal(2, await dbContext.SupplierStatementEntries.CountAsync(entity => entity.SourceDocId == posted.Id));
    }

    [Fact]
    public async Task Posting_ShouldRejectAllocationBeyondCurrentOpenAmount()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, references, "PR-PAY-0005", 100m, 100m);
        var services = CreateServices(dbContext);

        var firstDraft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.OutboundToParty,
                80m,
                new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, 80m)),
            "tester",
            CancellationToken.None);

        await services.PostingService.PostAsync(firstDraft.Id, "tester", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.PaymentService.CreateDraftAsync(
                BuildPaymentRequest(
                    references.Supplier.Id,
                    PaymentDirection.OutboundToParty,
                    30m,
                    new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, 30m)),
                "tester",
                CancellationToken.None));

        Assert.Contains("cannot exceed the open amount", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Posting_ShouldBeIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var receipt = await SeedPostedReceiptAsync(dbContext, references, "PR-PAY-0006", 100m, 100m);
        var services = CreateServices(dbContext);

        var draft = await services.PaymentService.CreateDraftAsync(
            BuildPaymentRequest(
                references.Supplier.Id,
                PaymentDirection.OutboundToParty,
                100m,
                new AllocationSeed(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, 100m)),
            "tester",
            CancellationToken.None);

        var firstPosted = await services.PostingService.PostAsync(draft.Id, "tester", CancellationToken.None);
        var secondPosted = await services.PostingService.PostAsync(draft.Id, "tester", CancellationToken.None);

        Assert.NotNull(firstPosted);
        Assert.NotNull(secondPosted);
        Assert.Equal(DocumentStatus.Posted, secondPosted!.Status);
        Assert.Equal(1, await dbContext.SupplierStatementEntries.CountAsync(entity => entity.SourceDocId == draft.Id));
    }

    private static UpsertPaymentRequest BuildPaymentRequest(
        Guid supplierId,
        PaymentDirection direction,
        decimal amount,
        params AllocationSeed[] allocations)
    {
        return new UpsertPaymentRequest(
            null,
            PaymentPartyType.Supplier,
            supplierId,
            direction,
            amount,
            DateTime.UtcNow.Date,
            "EGP",
            null,
            PaymentMethod.BankTransfer,
            "BANK-REF",
            "Payment notes",
            allocations.Select((allocation, index) => new UpsertPaymentAllocationRequest(
                allocation.TargetDocType,
                allocation.TargetDocId,
                null,
                allocation.AllocatedAmount,
                index + 1)).ToArray());
    }

    private static ServiceBundle CreateServices(AppDbContext dbContext)
    {
        var openBalanceService = new SupplierOpenBalanceService(dbContext);
        var allocationService = new PaymentAllocationService(openBalanceService);
        var queryService = new PaymentQueryService(dbContext);
        var paymentService = new PaymentService(dbContext, allocationService, queryService);
        var postingService = new SupplierPaymentPostingService(dbContext, queryService, allocationService, new SupplierStatementPostingService(dbContext));
        return new ServiceBundle(paymentService, postingService, openBalanceService);
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-payment-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<PaymentReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var supplier = new Supplier
        {
            Code = "SUP-PAY",
            Name = "Payment Supplier",
            StatementName = "Payment Supplier",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouse = new Warehouse
        {
            Code = "WH-PAY",
            Name = "Payment Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        return new PaymentReferences(supplier, warehouse);
    }

    private static async Task<PurchaseReceipt> SeedPostedReceiptAsync(
        AppDbContext dbContext,
        PaymentReferences references,
        string receiptNo,
        decimal payableAmount,
        decimal runningBalanceAfterReceipt)
    {
        var receipt = new PurchaseReceipt
        {
            ReceiptNo = receiptNo,
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            SupplierPayableAmount = payableAmount,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
        {
            SupplierId = references.Supplier.Id,
            EntryDate = receipt.ReceiptDate,
            SourceDocType = SupplierStatementSourceDocumentType.PurchaseReceipt,
            SourceDocId = receipt.Id,
            SourceLineId = receipt.Id,
            EffectType = SupplierStatementEffectType.PurchaseReceipt,
            Debit = 0m,
            Credit = payableAmount,
            RunningBalance = runningBalanceAfterReceipt,
            Currency = "EGP",
            Notes = $"Receipt {receiptNo}",
            CreatedBy = "seed"
        });

        await dbContext.SaveChangesAsync();
        return receipt;
    }

    private static async Task<ShortageResolution> SeedPostedFinancialResolutionAsync(
        AppDbContext dbContext,
        PaymentReferences references,
        string resolutionNo,
        decimal totalAmount,
        decimal runningBalanceAfterResolution)
    {
        var resolution = new ShortageResolution
        {
            ResolutionNo = resolutionNo,
            SupplierId = references.Supplier.Id,
            ResolutionType = ShortageResolutionType.Financial,
            ResolutionDate = DateTime.UtcNow.Date,
            TotalAmount = totalAmount,
            Currency = "EGP",
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.ShortageResolutions.Add(resolution);
        await dbContext.SaveChangesAsync();

        dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
        {
            SupplierId = references.Supplier.Id,
            EntryDate = resolution.ResolutionDate,
            SourceDocType = SupplierStatementSourceDocumentType.ShortageResolution,
            SourceDocId = resolution.Id,
            SourceLineId = Guid.NewGuid(),
            EffectType = SupplierStatementEffectType.ShortageFinancialResolution,
            Debit = totalAmount,
            Credit = 0m,
            RunningBalance = runningBalanceAfterResolution,
            Currency = "EGP",
            Notes = $"Resolution {resolutionNo}",
            CreatedBy = "seed"
        });

        await dbContext.SaveChangesAsync();
        return resolution;
    }

    private sealed record AllocationSeed(PaymentTargetDocumentType TargetDocType, Guid TargetDocId, decimal AllocatedAmount);
    private sealed record PaymentReferences(Supplier Supplier, Warehouse Warehouse);
    private sealed record ServiceBundle(IPaymentService PaymentService, ISupplierPaymentPostingService PostingService, ISupplierOpenBalanceService OpenBalanceService);
}
