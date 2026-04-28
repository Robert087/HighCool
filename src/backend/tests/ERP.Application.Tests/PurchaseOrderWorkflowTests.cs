using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.MasterData;
using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseOrders;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using ERP.Infrastructure.Statements;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class PurchaseOrderWorkflowTests
{
    [Fact]
    public void Validator_ShouldRequireHeaderAndLineData()
    {
        var validator = new UpsertPurchaseOrderRequestValidator();
        var result = validator.Validate(new UpsertPurchaseOrderRequest(
            null,
            Guid.Empty,
            null,
            null,
            null,
            []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "SupplierId");
        Assert.Contains(result.Errors, error => error.PropertyName == "OrderDate");
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("At least one line", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostingAndCancellation_ShouldTransitionStatuses()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = new PurchaseOrderService(dbContext);

        var created = await service.CreateDraftAsync(
            new UpsertPurchaseOrderRequest(
                "PO-TEST-0001",
                references.Supplier.Id,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.Date.AddDays(2),
                "PO notes",
                [
                    new UpsertPurchaseOrderLineRequest(1, references.Item.Id, 10m, references.Uom.Id, null)
                ]),
            "tester",
            CancellationToken.None);

        var postingService = new PurchaseOrderPostingService(dbContext, service);
        var cancellationService = new PurchaseOrderCancellationService(dbContext, service);

        var posted = await postingService.PostAsync(created.Id, "tester", CancellationToken.None);
        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);

        var canceled = await cancellationService.CancelAsync(created.Id, "tester", CancellationToken.None);
        Assert.NotNull(canceled);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
    }

    [Fact]
    public async Task ReceiptService_ShouldCreatePurchaseOrderLinkedDraftWithOrderedSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var purchaseOrder = await CreatePostedPurchaseOrderAsync(dbContext, references, orderedQty: 12m);
        var conversionService = new QuantityConversionService(dbContext);
        var receiptService = new PurchaseReceiptService(dbContext, conversionService);

        var receipt = await receiptService.CreateDraftAsync(
            new UpsertPurchaseReceiptDraftRequest(
                "PR-PO-0001",
                references.Supplier.Id,
                references.Warehouse.Id,
                purchaseOrder.Id,
                DateTime.UtcNow.Date,
                0m,
                "Receipt from PO",
                [
                    new UpsertPurchaseReceiptLineRequest(
                        1,
                        purchaseOrder.Lines.Single().Id,
                        references.Item.Id,
                        purchaseOrder.Lines.Single().OrderedQty,
                        5m,
                        references.Uom.Id,
                        null,
                        [])
                ]),
            "tester",
            CancellationToken.None);

        Assert.Equal(purchaseOrder.Id, receipt.PurchaseOrderId);
        Assert.Equal(purchaseOrder.PoNo, receipt.PurchaseOrderNo);
        Assert.Equal(purchaseOrder.Lines.Single().Id, receipt.Lines.Single().PurchaseOrderLineId);
        Assert.Equal(12m, receipt.Lines.Single().OrderedQtySnapshot);
    }

    [Fact]
    public async Task ReceiptPosting_ShouldRejectOverReceiptAgainstRemainingPurchaseOrderQuantity()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var purchaseOrder = await CreatePostedPurchaseOrderAsync(dbContext, references, orderedQty: 10m);

        var postedReceipt = new PurchaseReceipt
        {
            ReceiptNo = "PR-EXISTING-0001",
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            PurchaseOrderId = purchaseOrder.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Lines =
            [
                new PurchaseReceiptLine
                {
                    LineNo = 1,
                    PurchaseOrderLineId = purchaseOrder.Lines.Single().Id,
                    ItemId = references.Item.Id,
                    OrderedQtySnapshot = 10m,
                    ReceivedQty = 6m,
                    UomId = references.Uom.Id,
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.PurchaseReceipts.Add(postedReceipt);

        var draftReceipt = new PurchaseReceipt
        {
            ReceiptNo = "PR-DRAFT-0001",
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            PurchaseOrderId = purchaseOrder.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            Status = DocumentStatus.Draft,
            CreatedBy = "seed",
            Lines =
            [
                new PurchaseReceiptLine
                {
                    LineNo = 1,
                    PurchaseOrderLineId = purchaseOrder.Lines.Single().Id,
                    ItemId = references.Item.Id,
                    OrderedQtySnapshot = 10m,
                    ReceivedQty = 5m,
                    UomId = references.Uom.Id,
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.PurchaseReceipts.Add(draftReceipt);
        await dbContext.SaveChangesAsync();

        var conversionService = new QuantityConversionService(dbContext);
        var receiptService = new PurchaseReceiptService(dbContext, conversionService);
        var postingService = new PurchaseReceiptPostingService(
            dbContext,
            receiptService,
            new StockLedgerService(dbContext, conversionService),
            new ShortageDetectionService(dbContext, conversionService),
            conversionService,
            new SupplierStatementPostingService(dbContext));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(draftReceipt.Id, "tester", CancellationToken.None));

        Assert.Contains("remaining purchase order quantity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AvailableLinesForReceipt_ShouldDisappearOncePurchaseOrderIsFullyReceived()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var purchaseOrder = await CreatePostedPurchaseOrderAsync(dbContext, references, orderedQty: 10m);

        dbContext.PurchaseReceipts.Add(new PurchaseReceipt
        {
            ReceiptNo = "PR-FULL-0001",
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            PurchaseOrderId = purchaseOrder.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Lines =
            [
                new PurchaseReceiptLine
                {
                    LineNo = 1,
                    PurchaseOrderLineId = purchaseOrder.Lines.Single().Id,
                    ItemId = references.Item.Id,
                    OrderedQtySnapshot = 10m,
                    ReceivedQty = 4m,
                    UomId = references.Uom.Id,
                    CreatedBy = "seed"
                }
            ]
        });

        dbContext.PurchaseReceipts.Add(new PurchaseReceipt
        {
            ReceiptNo = "PR-FULL-0002",
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            PurchaseOrderId = purchaseOrder.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Lines =
            [
                new PurchaseReceiptLine
                {
                    LineNo = 1,
                    PurchaseOrderLineId = purchaseOrder.Lines.Single().Id,
                    ItemId = references.Item.Id,
                    OrderedQtySnapshot = 10m,
                    ReceivedQty = 6m,
                    UomId = references.Uom.Id,
                    CreatedBy = "seed"
                }
            ]
        });

        await dbContext.SaveChangesAsync();

        var service = new PurchaseOrderService(dbContext);

        var availableLines = await service.ListAvailableLinesForReceiptAsync(purchaseOrder.Id, CancellationToken.None);
        var reloadedOrder = await service.GetAsync(purchaseOrder.Id, CancellationToken.None);

        Assert.Empty(availableLines);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(PurchaseOrderReceiptProgressStatus.FullyReceived, reloadedOrder!.ReceiptProgressStatus);
        Assert.Equal(10m, reloadedOrder.Lines.Single().ReceivedQty);
        Assert.Equal(0m, reloadedOrder.Lines.Single().RemainingQty);
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-po-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<(Supplier Supplier, Warehouse Warehouse, Uom Uom, Item Item)> SeedReferencesAsync(AppDbContext dbContext)
    {
        var supplier = new Supplier
        {
            Code = "SUP-PO",
            Name = "Supplier PO",
            StatementName = "Supplier PO",
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
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Uoms.Add(uom);
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITM-PO",
            Name = "PO Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return (supplier, warehouse, uom, item);
    }

    private static async Task<PurchaseOrder> CreatePostedPurchaseOrderAsync(
        AppDbContext dbContext,
        (Supplier Supplier, Warehouse Warehouse, Uom Uom, Item Item) references,
        decimal orderedQty)
    {
        var purchaseOrder = new PurchaseOrder
        {
            PoNo = $"PO-{Guid.NewGuid():N}".Substring(0, 15),
            SupplierId = references.Supplier.Id,
            OrderDate = DateTime.UtcNow.Date,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed",
            Lines =
            [
                new PurchaseOrderLine
                {
                    LineNo = 1,
                    ItemId = references.Item.Id,
                    OrderedQty = orderedQty,
                    UomId = references.Uom.Id,
                    CreatedBy = "seed"
                }
            ]
        };

        dbContext.PurchaseOrders.Add(purchaseOrder);
        await dbContext.SaveChangesAsync();
        return purchaseOrder;
    }
}
