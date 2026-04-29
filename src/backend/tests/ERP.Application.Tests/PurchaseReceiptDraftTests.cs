using ERP.Application.Common.Exceptions;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.MasterData;
using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class PurchaseReceiptDraftTests
{
    [Fact]
    public void Validator_ShouldRequireHeaderAndLineData()
    {
        var validator = new UpsertPurchaseReceiptDraftRequestValidator();
        var model = new UpsertPurchaseReceiptDraftRequest(
            null,
            Guid.Empty,
            Guid.Empty,
            null,
            null,
            0m,
            null,
            []);

        var result = validator.Validate(model);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "SupplierId");
        Assert.Contains(result.Errors, error => error.PropertyName == "WarehouseId");
        Assert.Contains(result.Errors, error => error.PropertyName == "ReceiptDate");
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("At least one line", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRejectLineQuantityAndDuplicateComponentRows()
    {
        var validator = new UpsertPurchaseReceiptDraftRequestValidator();
        var duplicateComponentId = Guid.NewGuid();

        var model = new UpsertPurchaseReceiptDraftRequest(
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            DateTime.UtcNow,
            0m,
            null,
            [
                new UpsertPurchaseReceiptLineRequest(
                    1,
                    null,
                    Guid.NewGuid(),
                    null,
                    0m,
                    Guid.NewGuid(),
                    null,
                    [
                        new UpsertPurchaseReceiptLineComponentRequest(duplicateComponentId, 0m, Guid.NewGuid(), null, null),
                        new UpsertPurchaseReceiptLineComponentRequest(duplicateComponentId, 1m, Guid.NewGuid(), null, null)
                    ])
            ]);

        var result = validator.Validate(model);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.Contains("ReceivedQty", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("duplicate component item rows", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Service_ShouldRejectDuplicateReceiptNumbers()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);

        dbContext.PurchaseReceipts.Add(new PurchaseReceipt
        {
            ReceiptNo = "PRD-TEST-0001",
            SupplierId = references.Supplier.Id,
            WarehouseId = references.Warehouse.Id,
            ReceiptDate = DateTime.UtcNow.Date,
            Status = DocumentStatus.Draft,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = BuildDraftRequest(references, "PRD-TEST-0001");

        var exception = await Assert.ThrowsAsync<DuplicateEntityException>(() =>
            service.CreateDraftAsync(request, "tester", CancellationToken.None));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_ShouldRejectEditingNonDraftReceipts()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);

        var receipt = new PurchaseReceipt
        {
          ReceiptNo = "PRD-LOCKED-0001",
          SupplierId = references.Supplier.Id,
          WarehouseId = references.Warehouse.Id,
          ReceiptDate = DateTime.UtcNow.Date,
          Status = DocumentStatus.Posted,
          CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = BuildDraftRequest(references, "PRD-LOCKED-0001");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateDraftAsync(receipt.Id, request, "tester", CancellationToken.None));

        Assert.Contains("Only Draft purchase receipts can be edited", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_ShouldAutoFillExpectedAndActualComponentsFromItemDefinition()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext, hasBom: true, componentQty: 2m);
        var service = CreateService(dbContext);

        var receipt = await service.CreateDraftAsync(
            new UpsertPurchaseReceiptDraftRequest(
                "PRD-AUTO-0001",
                references.Supplier.Id,
                references.Warehouse.Id,
                null,
                DateTime.UtcNow.Date,
                0m,
                "Auto-fill test",
                [
                    new UpsertPurchaseReceiptLineRequest(
                        1,
                        null,
                        references.Item.Id,
                        null,
                        3m,
                        references.Uom.Id,
                        null,
                        [])
                ]),
            "tester",
            CancellationToken.None);

        var component = Assert.Single(receipt.Lines.Single().Components);
        Assert.Equal(references.ComponentItem.Id, component.ComponentItemId);
        Assert.Equal(6m, component.ExpectedQty);
        Assert.Equal(6m, component.ActualReceivedQty);
        Assert.Equal(references.Uom.Id, component.UomId);
    }

    [Fact]
    public async Task Service_ShouldRecalculateExpectedQtyAndPreserveEditedActualQtyOnUpdate()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext, hasBom: true, componentQty: 2m);
        var service = CreateService(dbContext);

        var created = await service.CreateDraftAsync(
            new UpsertPurchaseReceiptDraftRequest(
                "PRD-AUTO-0002",
                references.Supplier.Id,
                references.Warehouse.Id,
                null,
                DateTime.UtcNow.Date,
                0m,
                null,
                [
                    new UpsertPurchaseReceiptLineRequest(
                        1,
                        null,
                        references.Item.Id,
                        null,
                        3m,
                        references.Uom.Id,
                        null,
                        [
                            new UpsertPurchaseReceiptLineComponentRequest(references.ComponentItem.Id, 7m, references.Uom.Id, null, "Edited actual")
                        ])
                ]),
            "tester",
            CancellationToken.None);

        var updated = await service.UpdateDraftAsync(
            created.Id,
            new UpsertPurchaseReceiptDraftRequest(
                created.ReceiptNo,
                references.Supplier.Id,
                references.Warehouse.Id,
                null,
                DateTime.UtcNow.Date,
                0m,
                null,
                [
                    new UpsertPurchaseReceiptLineRequest(
                        1,
                        null,
                        references.Item.Id,
                        null,
                        2m,
                        references.Uom.Id,
                        null,
                        [
                            new UpsertPurchaseReceiptLineComponentRequest(references.ComponentItem.Id, 7m, references.Uom.Id, null, "Edited actual")
                        ])
                ]),
            "tester",
            CancellationToken.None);

        Assert.NotNull(updated);
        var component = Assert.Single(updated!.Lines.Single().Components);
        Assert.Equal(4m, component.ExpectedQty);
        Assert.Equal(7m, component.ActualReceivedQty);
    }

    [Fact]
    public async Task Service_ShouldRejectComponentRowsOutsideSelectedItemBom()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDraftAsync(
                new UpsertPurchaseReceiptDraftRequest(
                    "PRD-BAD-0001",
                    references.Supplier.Id,
                    references.Warehouse.Id,
                    null,
                    DateTime.UtcNow.Date,
                    0m,
                    null,
                    [
                        new UpsertPurchaseReceiptLineRequest(
                            1,
                            null,
                            references.Item.Id,
                            null,
                            2m,
                            references.Uom.Id,
                            null,
                            [
                                new UpsertPurchaseReceiptLineComponentRequest(references.ComponentItem.Id, 2m, references.Uom.Id, null, null)
                            ])
                    ]),
                "tester",
                CancellationToken.None));

        Assert.Contains("not defined on the selected item", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_ShouldAllowShortageWithoutShortageReason()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext, hasBom: true, componentQty: 2m);
        var service = CreateService(dbContext);

        var receipt = await service.CreateDraftAsync(
            new UpsertPurchaseReceiptDraftRequest(
                "PRD-SHORT-0001",
                references.Supplier.Id,
                references.Warehouse.Id,
                null,
                DateTime.UtcNow.Date,
                0m,
                null,
                [
                    new UpsertPurchaseReceiptLineRequest(
                        1,
                        null,
                        references.Item.Id,
                        null,
                        3m,
                        references.Uom.Id,
                        null,
                        [
                            new UpsertPurchaseReceiptLineComponentRequest(references.ComponentItem.Id, 5m, references.Uom.Id, null, null)
                        ])
                ]),
            "tester",
            CancellationToken.None);

        var component = Assert.Single(receipt.Lines.Single().Components);
        Assert.Equal(5m, component.ActualReceivedQty);
        Assert.Null(component.ShortageReasonCodeId);
    }

    [Fact]
    public async Task Service_ShouldShowContextWhenReceiptLineUomConversionIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext, includeAltReceiptUom: true);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDraftAsync(
                new UpsertPurchaseReceiptDraftRequest(
                    "PRD-UOM-0001",
                    references.Supplier.Id,
                    references.Warehouse.Id,
                    null,
                    DateTime.UtcNow.Date,
                    0m,
                    null,
                    [
                        new UpsertPurchaseReceiptLineRequest(
                            1,
                            null,
                            references.Item.Id,
                            null,
                            2m,
                            references.AltReceiptUom!.Id,
                            null,
                            [])
                    ]),
                "tester",
                CancellationToken.None));

        Assert.Contains("Purchase receipt line 1", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(references.Item.Code, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("global UOM conversion", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PurchaseReceiptService CreateService(AppDbContext dbContext)
    {
        var quantityConversionService = new QuantityConversionService(dbContext);
        var organizationId = dbContext.Organizations.IgnoreQueryFilters().Select(entity => entity.Id).Single();
        return new PurchaseReceiptService(dbContext, TestOrganizationContext.CreateExecutionContext(organizationId), quantityConversionService);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var executionContext = TestOrganizationContext.CreateExecutionContext();
        var dbContext = new AppDbContext(options, executionContext);
        TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext).GetAwaiter().GetResult();
        return dbContext;
    }

    private static async Task<(Supplier Supplier, Warehouse Warehouse, Uom Uom, Item Item, Item ComponentItem, Uom? AltReceiptUom)> SeedReferencesAsync(
        AppDbContext dbContext,
        bool hasBom = false,
        decimal componentQty = 1m,
        bool includeAltReceiptUom = false)
    {
        var supplier = new Supplier
        {
            Code = "SUP-01",
            Name = "Supplier 01",
            StatementName = "Supplier 01",
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

        var altReceiptUom = includeAltReceiptUom
            ? new Uom
            {
                Code = "BOX",
                Name = "Box",
                Precision = 0,
                AllowsFraction = false,
                IsActive = true,
                CreatedBy = "seed"
            }
            : null;

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Uoms.Add(uom);
        if (altReceiptUom is not null)
        {
            dbContext.Uoms.Add(altReceiptUom);
        }
        await dbContext.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITM-01",
            Name = "Main Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = hasBom,
            CreatedBy = "seed"
        };

        var componentItem = new Item
        {
            Code = "CMP-01",
            Name = "Component Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(item, componentItem);
        await dbContext.SaveChangesAsync();

        if (hasBom)
        {
            dbContext.ItemComponents.Add(new ItemComponent
            {
                ItemId = item.Id,
                ComponentItemId = componentItem.Id,
                UomId = uom.Id,
                Quantity = componentQty,
                CreatedBy = "seed"
            });
            await dbContext.SaveChangesAsync();
        }

        return (supplier, warehouse, uom, item, componentItem, altReceiptUom);
    }

    private static UpsertPurchaseReceiptDraftRequest BuildDraftRequest(
        (Supplier Supplier, Warehouse Warehouse, Uom Uom, Item Item, Item ComponentItem, Uom? AltReceiptUom) references,
        string receiptNo)
    {
        return new UpsertPurchaseReceiptDraftRequest(
            receiptNo,
            references.Supplier.Id,
            references.Warehouse.Id,
            null,
            DateTime.UtcNow.Date,
            0m,
            "Draft notes",
            [
                new UpsertPurchaseReceiptLineRequest(
                    1,
                    null,
                    references.Item.Id,
                    10m,
                    9m,
                    references.Uom.Id,
                    "Line notes",
                    [
                        new UpsertPurchaseReceiptLineComponentRequest(references.ComponentItem.Id, 9m, references.Uom.Id, null, "Component note")
                    ])
            ]);
    }
}
