using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.MasterData.Items;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Items;

public sealed class ItemService(AppDbContext dbContext) : IItemService
{
    public async Task<PagedResult<ItemDto>> ListAsync(ItemListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var items = dbContext.Items
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            items = items.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search) ||
                entity.BaseUom!.Code.Contains(search) ||
                (entity.Category != null && entity.Category.Name.Contains(search)) ||
                (entity.DefaultWarehouse != null && entity.DefaultWarehouse.Code.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            items = items.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        if (query.IsSellable.HasValue)
        {
            items = items.Where(entity => entity.IsSellable == query.IsSellable.Value);
        }

        if (query.CategoryId.HasValue)
        {
            items = items.Where(entity => entity.CategoryId == query.CategoryId.Value);
        }

        if (query.BaseUomId.HasValue)
        {
            items = items.Where(entity => entity.BaseUomId == query.BaseUomId.Value);
        }

        items = ApplySorting(items, query);

        var totalCount = await items.CountAsync(cancellationToken);
        var rows = await items
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new ItemDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.CategoryId,
                entity.Category != null ? entity.Category.Code : null,
                entity.Category != null ? entity.Category.Name : null,
                entity.BaseUomId,
                entity.BaseUom!.Code,
                entity.BaseUom.Name,
                entity.DefaultWarehouseId,
                entity.DefaultWarehouse != null ? entity.DefaultWarehouse.Code : null,
                entity.DefaultWarehouse != null ? entity.DefaultWarehouse.Name : null,
                entity.MinimumStockQuantity,
                entity.IsActive,
                entity.IsSellable,
                entity.HasComponents,
                Array.Empty<ItemComponentDto>(),
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ItemDto>(
            rows,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new { query.Search, query.IsActive, query.IsSellable, query.CategoryId, query.BaseUomId },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public Task<ItemDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Items
            .AsNoTracking()
            .Include(entity => entity.Category)
            .Include(entity => entity.BaseUom)
            .Include(entity => entity.DefaultWarehouse)
            .Include(entity => entity.Components)
                .ThenInclude(entity => entity.ComponentItem)
                    .ThenInclude(entity => entity!.BaseUom)
            .Include(entity => entity.Components)
                .ThenInclude(entity => entity.Uom)
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ItemDto> CreateAsync(UpsertItemRequest request, string actor, CancellationToken cancellationToken)
    {
        await EnsureCodeIsUniqueAsync(request.Code, null, cancellationToken);
        await EnsureBaseUomExistsAsync(request.BaseUomId, cancellationToken);
        await EnsureCategoryCanBeAssignedAsync(request.CategoryId, cancellationToken);
        await EnsureDefaultWarehouseCanBeAssignedAsync(request.DefaultWarehouseId, cancellationToken);

        var item = new Item
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            BaseUomId = request.BaseUomId,
            DefaultWarehouseId = request.DefaultWarehouseId,
            MinimumStockQuantity = request.MinimumStockQuantity,
            IsActive = request.IsActive,
            IsSellable = request.IsSellable,
            HasComponents = request.HasComponents,
            CreatedBy = actor
        };

        dbContext.Items.Add(item);
        await UpsertComponentsAsync(item, request.Components, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(item.Id, cancellationToken);
    }

    public async Task<ItemDto?> UpdateAsync(Guid id, UpsertItemRequest request, string actor, CancellationToken cancellationToken)
    {
        var item = await dbContext.Items.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        await EnsureCodeIsUniqueAsync(request.Code, id, cancellationToken);
        await EnsureBaseUomExistsAsync(request.BaseUomId, cancellationToken);
        await EnsureCategoryCanBeAssignedAsync(request.CategoryId, cancellationToken);
        await EnsureDefaultWarehouseCanBeAssignedAsync(request.DefaultWarehouseId, cancellationToken);

        item.Code = request.Code.Trim();
        item.Name = request.Name.Trim();
        item.CategoryId = request.CategoryId;
        item.BaseUomId = request.BaseUomId;
        item.DefaultWarehouseId = request.DefaultWarehouseId;
        item.MinimumStockQuantity = request.MinimumStockQuantity;
        item.IsActive = request.IsActive;
        item.IsSellable = request.IsSellable;
        item.HasComponents = request.HasComponents;
        item.UpdatedBy = actor;

        await ReplaceComponentsAsync(item, request.Components, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(item.Id, cancellationToken);
    }

    public async Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var item = await dbContext.Items.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (item is null)
        {
            return false;
        }

        if (!item.IsActive)
        {
            return true;
        }

        item.IsActive = false;
        item.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();
        var exists = await dbContext.Items.AnyAsync(
            entity => entity.Code == normalizedCode && entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException($"Item code '{normalizedCode}' already exists.");
        }
    }

    private async Task EnsureBaseUomExistsAsync(Guid baseUomId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Uoms.AnyAsync(entity => entity.Id == baseUomId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Base UOM was not found.");
        }
    }

    private async Task EnsureCategoryCanBeAssignedAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await dbContext.ItemCategories.AnyAsync(
            entity => entity.Id == categoryId.Value && entity.IsActive,
            cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Item category was not found or is inactive.");
        }
    }

    private async Task EnsureDefaultWarehouseCanBeAssignedAsync(Guid? warehouseId, CancellationToken cancellationToken)
    {
        if (!warehouseId.HasValue)
        {
            return;
        }

        var exists = await dbContext.Warehouses.AnyAsync(
            entity => entity.Id == warehouseId.Value && entity.IsActive,
            cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Default warehouse was not found or is inactive.");
        }
    }

    private async Task<ItemDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await GetAsync(id, cancellationToken);
        return item ?? throw new InvalidOperationException("Item was not found after save.");
    }

    private async Task ReplaceComponentsAsync(
        Item item,
        IReadOnlyList<UpsertItemComponentRequest> components,
        string actor,
        CancellationToken cancellationToken)
    {
        var existingRows = await dbContext.ItemComponents
            .Where(entity => entity.ItemId == item.Id)
            .ToListAsync(cancellationToken);

        dbContext.ItemComponents.RemoveRange(existingRows);
        await UpsertComponentsAsync(item, components, actor, cancellationToken);
    }

    private async Task UpsertComponentsAsync(
        Item item,
        IReadOnlyList<UpsertItemComponentRequest> components,
        string actor,
        CancellationToken cancellationToken)
    {
        if (components.Count == 0)
        {
            return;
        }

        if (!item.HasComponents)
        {
            throw new InvalidOperationException("Component rows can only be provided when the item is marked as having components.");
        }

        var duplicateComponentIds = components
            .GroupBy(component => component.ComponentItemId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateComponentIds.Length > 0)
        {
            throw new DuplicateEntityException("Duplicate component rows are not allowed for the same item.");
        }

        var componentIds = components.Select(component => component.ComponentItemId).Distinct().ToArray();
        var uomIds = components.Select(component => component.UomId).Distinct().Append(item.BaseUomId).ToArray();

        var componentItems = await dbContext.Items
            .AsNoTracking()
            .Include(entity => entity.BaseUom)
            .Where(entity => componentIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);

        var uoms = await dbContext.Uoms
            .AsNoTracking()
            .Where(entity => uomIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);

        foreach (var component in components)
        {
            if (component.ComponentItemId == item.Id)
            {
                throw new InvalidOperationException("Item components cannot reference the same item as both parent and component.");
            }

            if (!componentItems.TryGetValue(component.ComponentItemId, out var componentItem))
            {
                throw new InvalidOperationException("One or more component items were not found.");
            }

            if (!uoms.ContainsKey(component.UomId))
            {
                throw new InvalidOperationException("One or more component UOM references were not found.");
            }

            if (component.Quantity <= 0m)
            {
                throw new InvalidOperationException("Component quantity must be greater than zero.");
            }

            if (component.UomId != componentItem.BaseUomId)
            {
                var conversionExists = await dbContext.UomConversions.AnyAsync(
                    entity => entity.FromUomId == component.UomId &&
                              entity.ToUomId == componentItem.BaseUomId &&
                              entity.IsActive,
                    cancellationToken);

                if (!conversionExists)
                {
                    throw new InvalidOperationException("A global UOM conversion is required for component quantities that do not use the component item's base UOM.");
                }
            }

            dbContext.ItemComponents.Add(new ItemComponent
            {
                ItemId = item.Id,
                ComponentItemId = component.ComponentItemId,
                UomId = component.UomId,
                Quantity = component.Quantity,
                CreatedBy = actor
            });
        }
    }

    private static ItemDto ToDto(Item entity)
    {
        return new ItemDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.CategoryId,
            entity.Category?.Code,
            entity.Category?.Name,
            entity.BaseUomId,
            entity.BaseUom?.Code ?? string.Empty,
            entity.BaseUom?.Name ?? string.Empty,
            entity.DefaultWarehouseId,
            entity.DefaultWarehouse?.Code,
            entity.DefaultWarehouse?.Name,
            entity.MinimumStockQuantity,
            entity.IsActive,
            entity.IsSellable,
            entity.HasComponents,
            entity.Components
                .OrderBy(component => component.ComponentItem!.Name)
                .ThenBy(component => component.ComponentItem!.Code)
                .Select(component => new ItemComponentDto(
                    component.Id,
                    component.ItemId,
                    component.ComponentItemId,
                    component.ComponentItem?.Code ?? string.Empty,
                    component.ComponentItem?.Name ?? string.Empty,
                    component.ComponentItem?.BaseUomId ?? Guid.Empty,
                    component.ComponentItem?.BaseUom?.Code ?? string.Empty,
                    component.UomId,
                    component.Uom?.Code ?? string.Empty,
                    component.Uom?.Name ?? string.Empty,
                    component.Quantity,
                    component.CreatedAt,
                    component.UpdatedAt))
                .ToArray(),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static IQueryable<Item> ApplySorting(IQueryable<Item> query, ItemListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("code", true) => query.OrderBy(entity => entity.Code).ThenBy(entity => entity.Name),
            ("code", false) => query.OrderByDescending(entity => entity.Code).ThenByDescending(entity => entity.Name),
            ("category", true) => query.OrderBy(entity => entity.Category!.Name).ThenBy(entity => entity.Code),
            ("category", false) => query.OrderByDescending(entity => entity.Category!.Name).ThenByDescending(entity => entity.Code),
            ("baseUom", true) => query.OrderBy(entity => entity.BaseUom!.Code).ThenBy(entity => entity.Code),
            ("baseUom", false) => query.OrderByDescending(entity => entity.BaseUom!.Code).ThenByDescending(entity => entity.Code),
            ("minimumStockQuantity", true) => query.OrderBy(entity => entity.MinimumStockQuantity).ThenBy(entity => entity.Code),
            ("minimumStockQuantity", false) => query.OrderByDescending(entity => entity.MinimumStockQuantity).ThenByDescending(entity => entity.Code),
            ("createdAt", true) => query.OrderBy(entity => entity.CreatedAt).ThenBy(entity => entity.Code),
            ("createdAt", false) => query.OrderByDescending(entity => entity.CreatedAt).ThenByDescending(entity => entity.Code),
            _ when ascending => query.OrderBy(entity => entity.Name).ThenBy(entity => entity.Code),
            _ => query.OrderByDescending(entity => entity.Name).ThenByDescending(entity => entity.Code)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "code" => "code",
            "category" => "category",
            "baseUom" => "baseUom",
            "minimumStockQuantity" => "minimumStockQuantity",
            "createdAt" => "createdAt",
            _ => "name"
        };
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
