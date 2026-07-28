using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.Items;
using ERP.Application.MasterData.UomConversions;
using ERP.Domain.MasterData;
using ERP.Infrastructure.MasterData.Items;
using ERP.Infrastructure.MasterData.UomConversions;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class ItemMasterDataRulesTests
{
    [Fact]
    public void ItemValidator_ShouldRequireRowsWhenHasComponentsIsTrue()
    {
        var validator = new UpsertItemRequestValidator();
        var model = new UpsertItemRequest("ITM-1", "Assembly", null, Guid.NewGuid(), null, 0m, true, true, true, []);

        var result = validator.Validate(model);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("at least one component row", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ItemService_ShouldRejectDuplicateComponentRows()
    {
        await using var dbContext = CreateDbContext();
        var (uom, parentItem, componentItem) = await SeedItemsAsync(dbContext);
        var service = new ItemService(dbContext);

        var request = new UpsertItemRequest(
            "ITM-NEW",
            "New Assembly",
            null,
            uom.Id,
            null,
            0m,
            true,
            true,
            true,
            [
                new UpsertItemComponentRequest(componentItem.Id, uom.Id, 1m),
                new UpsertItemComponentRequest(componentItem.Id, uom.Id, 2m)
            ]);

        var exception = await Assert.ThrowsAsync<DuplicateEntityException>(() =>
            service.CreateAsync(request, "tester", CancellationToken.None));

        Assert.Contains("duplicate component rows", exception.Message, StringComparison.OrdinalIgnoreCase);
        _ = parentItem;
    }

    [Fact]
    public async Task ItemService_ShouldRejectSelfReferencingComponentRows()
    {
        await using var dbContext = CreateDbContext();
        var (uom, parentItem, _) = await SeedItemsAsync(dbContext);
        var service = new ItemService(dbContext);

        var request = new UpsertItemRequest(
            "ITM-SELF",
            "Self Assembly",
            null,
            uom.Id,
            null,
            0m,
            true,
            true,
            true,
            [new UpsertItemComponentRequest(parentItem.Id, uom.Id, 1m)]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(parentItem.Id, request, "tester", CancellationToken.None));

        Assert.Contains("same item", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ItemService_ShouldRequireGlobalConversionWhenComponentUomDiffersFromBase()
    {
        await using var dbContext = CreateDbContext();
        var (pieceUom, parentItem, componentItem) = await SeedItemsAsync(dbContext);

        var boxUom = new Uom
        {
            Code = "BOX",
            Name = "Box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.Add(boxUom);
        await dbContext.SaveChangesAsync();

        var service = new ItemService(dbContext);
        var request = new UpsertItemRequest(
            "ITM-CONV",
            "Assembly With Box",
            null,
            pieceUom.Id,
            null,
            0m,
            true,
            true,
            true,
            [new UpsertItemComponentRequest(componentItem.Id, boxUom.Id, 1m)]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(request, "tester", CancellationToken.None));

        Assert.Contains("global UOM conversion is required", exception.Message, StringComparison.OrdinalIgnoreCase);
        _ = parentItem;
    }

    [Fact]
    public async Task ItemService_ShouldRejectInactiveCategoryAssignment()
    {
        await using var dbContext = CreateDbContext();
        var (uom, _, _) = await SeedItemsAsync(dbContext);
        var inactiveCategory = new ItemCategory
        {
            Code = "CAT-INACTIVE",
            Name = "Inactive Category",
            IsActive = false,
            CreatedBy = "seed"
        };

        dbContext.ItemCategories.Add(inactiveCategory);
        await dbContext.SaveChangesAsync();

        var service = new ItemService(dbContext);
        var request = new UpsertItemRequest(
            "ITM-CAT",
            "Categorized Item",
            inactiveCategory.Id,
            uom.Id,
            null,
            0m,
            true,
            true,
            false,
            []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(request, "tester", CancellationToken.None));

        Assert.Contains("item category", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inactive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ItemService_ShouldPersistInventoryDefaultsAndReturnPagedLists()
    {
        await using var dbContext = CreateDbContext();
        var (uom, _, _) = await SeedItemsAsync(dbContext);
        var category = new ItemCategory
        {
            Code = "CAT-SPARES",
            Name = "Spares",
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

        dbContext.ItemCategories.Add(category);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var service = new ItemService(dbContext);
        await service.CreateAsync(
            new UpsertItemRequest(
                "ITM-FILTER-1",
                "Filtered Item One",
                category.Id,
                uom.Id,
                warehouse.Id,
                5.5m,
                true,
                true,
                false,
                []),
            "tester",
            CancellationToken.None);

        await service.CreateAsync(
            new UpsertItemRequest(
                "ITM-FILTER-2",
                "Filtered Item Two",
                null,
                uom.Id,
                null,
                0m,
                true,
                false,
                false,
                []),
            "tester",
            CancellationToken.None);

        var result = await service.ListAsync(
            new ItemListQuery(null, true, true, category.Id, null, Page: 1, PageSize: 10, SortBy: "code"),
            CancellationToken.None);

        var row = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(category.Id, row.CategoryId);
        Assert.Equal("CAT-SPARES", row.CategoryCode);
        Assert.Equal(warehouse.Id, row.DefaultWarehouseId);
        Assert.Equal(5.5m, row.MinimumStockQuantity);
    }

    [Fact]
    public async Task UomConversionService_ShouldRejectDuplicateActivePair()
    {
        await using var dbContext = CreateDbContext();
        var (uom, _, _) = await SeedItemsAsync(dbContext);

        var alternateUom = new Uom
        {
            Code = "BOX",
            Name = "Box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.Add(alternateUom);
        await dbContext.SaveChangesAsync();

        dbContext.UomConversions.Add(new UomConversion
        {
            FromUomId = alternateUom.Id,
            ToUomId = uom.Id,
            Factor = 12m,
            RoundingMode = RoundingMode.Round,
            IsActive = true,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = new UomConversionService(dbContext);
        var request = new UpsertUomConversionRequest(
            alternateUom.Id,
            uom.Id,
            24m,
            RoundingMode.Round,
            true);

        var exception = await Assert.ThrowsAsync<DuplicateEntityException>(() =>
            service.CreateAsync(request, "tester", CancellationToken.None));

        Assert.Contains("active conversion", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(Uom uom, Item parentItem, Item componentItem)> SeedItemsAsync(AppDbContext dbContext)
    {
        var uom = new Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.Add(uom);
        await dbContext.SaveChangesAsync();

        var parentItem = new Item
        {
            Code = "ITM-PARENT",
            Name = "Parent Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = "seed"
        };

        var componentItem = new Item
        {
            Code = "ITM-COMP",
            Name = "Component Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(parentItem, componentItem);
        await dbContext.SaveChangesAsync();

        return (uom, parentItem, componentItem);
    }
}
