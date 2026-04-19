using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.ItemComponents;
using ERP.Application.MasterData.ItemUomConversions;
using ERP.Application.MasterData.Items;
using ERP.Domain.MasterData;
using ERP.Infrastructure.MasterData.ItemComponents;
using ERP.Infrastructure.MasterData.ItemUomConversions;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class ItemMasterDataRulesTests
{
    [Fact]
    public void ItemValidator_ShouldRequireSellableOrComponentRole()
    {
        var validator = new UpsertItemRequestValidator();
        var model = new UpsertItemRequest("ITM-1", "Item 1", Guid.NewGuid(), true, false, false);

        var result = validator.Validate(model);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("sellable, component, or both", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ItemComponentService_ShouldRejectDuplicateParentComponentPair()
    {
        await using var dbContext = CreateDbContext();
        var (uom, parentItem, componentItem) = await SeedItemsAsync(dbContext);

        dbContext.ItemComponents.Add(new ItemComponent
        {
            ParentItemId = parentItem.Id,
            ComponentItemId = componentItem.Id,
            Quantity = 1m,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = new ItemComponentService(dbContext);
        var request = new UpsertItemComponentRequest(parentItem.Id, componentItem.Id, 2m);

        var exception = await Assert.ThrowsAsync<DuplicateEntityException>(() =>
            service.CreateAsync(request, "tester", CancellationToken.None));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
        _ = uom;
    }

    [Fact]
    public async Task ItemComponentService_ShouldRejectSelfReference()
    {
        await using var dbContext = CreateDbContext();
        var (_, parentItem, _) = await SeedItemsAsync(dbContext);
        var service = new ItemComponentService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new UpsertItemComponentRequest(parentItem.Id, parentItem.Id, 1m), "tester", CancellationToken.None));

        Assert.Contains("different", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ItemUomConversionService_ShouldRejectDuplicateActivePair()
    {
        await using var dbContext = CreateDbContext();
        var (uom, item, _) = await SeedItemsAsync(dbContext);
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

        dbContext.ItemUomConversions.Add(new ItemUomConversion
        {
            ItemId = item.Id,
            FromUomId = uom.Id,
            ToUomId = alternateUom.Id,
            Factor = 12m,
            RoundingMode = RoundingMode.Round,
            MinFraction = 0m,
            IsActive = true,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = new ItemUomConversionService(dbContext);
        var request = new UpsertItemUomConversionRequest(
            item.Id,
            uom.Id,
            alternateUom.Id,
            24m,
            RoundingMode.Round,
            0m,
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
            IsComponent = false,
            CreatedBy = "seed"
        };

        var componentItem = new Item
        {
            Code = "ITM-COMP",
            Name = "Component Item",
            BaseUomId = uom.Id,
            IsActive = true,
            IsSellable = false,
            IsComponent = true,
            CreatedBy = "seed"
        };

        dbContext.Items.AddRange(parentItem, componentItem);
        await dbContext.SaveChangesAsync();

        return (uom, parentItem, componentItem);
    }
}
