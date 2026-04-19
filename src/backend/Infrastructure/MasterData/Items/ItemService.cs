using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.Items;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Items;

public sealed class ItemService(AppDbContext dbContext) : IItemService
{
    public async Task<IReadOnlyList<ItemDto>> ListAsync(ItemListQuery query, CancellationToken cancellationToken)
    {
        var items = dbContext.Items
            .AsNoTracking()
            .Include(entity => entity.BaseUom)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            items = items.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search) ||
                entity.BaseUom!.Code.Contains(search));
        }

        if (query.IsActive.HasValue)
        {
            items = items.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        return await items
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.Code)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);
    }

    public Task<ItemDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Items
            .AsNoTracking()
            .Include(entity => entity.BaseUom)
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ItemDto> CreateAsync(UpsertItemRequest request, string actor, CancellationToken cancellationToken)
    {
        await EnsureCodeIsUniqueAsync(request.Code, null, cancellationToken);
        await EnsureBaseUomExistsAsync(request.BaseUomId, cancellationToken);

        var item = new Item
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            BaseUomId = request.BaseUomId,
            IsActive = request.IsActive,
            IsSellable = request.IsSellable,
            IsComponent = request.IsComponent,
            CreatedBy = actor
        };

        dbContext.Items.Add(item);
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

        item.Code = request.Code.Trim();
        item.Name = request.Name.Trim();
        item.BaseUomId = request.BaseUomId;
        item.IsActive = request.IsActive;
        item.IsSellable = request.IsSellable;
        item.IsComponent = request.IsComponent;
        item.UpdatedBy = actor;

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

    private async Task<ItemDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await GetAsync(id, cancellationToken);
        return item ?? throw new InvalidOperationException("Item was not found after save.");
    }

    private static ItemDto ToDto(Item entity)
    {
        return new ItemDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.BaseUomId,
            entity.BaseUom?.Code ?? string.Empty,
            entity.BaseUom?.Name ?? string.Empty,
            entity.IsActive,
            entity.IsSellable,
            entity.IsComponent,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
