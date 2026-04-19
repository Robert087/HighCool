using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.ItemComponents;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.ItemComponents;

public sealed class ItemComponentService(AppDbContext dbContext) : IItemComponentService
{
    public async Task<IReadOnlyList<ItemComponentDto>> ListAsync(ItemComponentListQuery query, CancellationToken cancellationToken)
    {
        var components = IncludeItems();

        if (query.ParentItemId.HasValue)
        {
            components = components.Where(entity => entity.ParentItemId == query.ParentItemId.Value);
        }

        if (query.ComponentItemId.HasValue)
        {
            components = components.Where(entity => entity.ComponentItemId == query.ComponentItemId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            components = components.Where(entity =>
                entity.ParentItem!.Code.Contains(search) ||
                entity.ParentItem.Name.Contains(search) ||
                entity.ComponentItem!.Code.Contains(search) ||
                entity.ComponentItem.Name.Contains(search));
        }

        return await components
            .OrderBy(entity => entity.ParentItem!.Name)
            .ThenBy(entity => entity.ComponentItem!.Name)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);
    }

    public Task<ItemComponentDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return IncludeItems()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ItemComponentDto> CreateAsync(UpsertItemComponentRequest request, string actor, CancellationToken cancellationToken)
    {
        await ValidateRelationshipAsync(request, null, cancellationToken);

        var component = new ItemComponent
        {
            ParentItemId = request.ParentItemId,
            ComponentItemId = request.ComponentItemId,
            Quantity = request.Quantity,
            CreatedBy = actor
        };

        dbContext.ItemComponents.Add(component);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(component.Id, cancellationToken);
    }

    public async Task<ItemComponentDto?> UpdateAsync(Guid id, UpsertItemComponentRequest request, string actor, CancellationToken cancellationToken)
    {
        var component = await dbContext.ItemComponents.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (component is null)
        {
            return null;
        }

        await ValidateRelationshipAsync(request, id, cancellationToken);

        component.ParentItemId = request.ParentItemId;
        component.ComponentItemId = request.ComponentItemId;
        component.Quantity = request.Quantity;
        component.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(component.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var component = await dbContext.ItemComponents.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (component is null)
        {
            return false;
        }

        dbContext.ItemComponents.Remove(component);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<ItemComponent> IncludeItems()
    {
        return dbContext.ItemComponents
            .AsNoTracking()
            .Include(entity => entity.ParentItem)
            .Include(entity => entity.ComponentItem);
    }

    private async Task ValidateRelationshipAsync(UpsertItemComponentRequest request, Guid? currentId, CancellationToken cancellationToken)
    {
        if (request.ParentItemId == request.ComponentItemId)
        {
            throw new InvalidOperationException("Parent item and component item must be different.");
        }

        var parent = await dbContext.Items.SingleOrDefaultAsync(entity => entity.Id == request.ParentItemId, cancellationToken);
        if (parent is null)
        {
            throw new InvalidOperationException("Parent item was not found.");
        }

        var componentItem = await dbContext.Items.SingleOrDefaultAsync(entity => entity.Id == request.ComponentItemId, cancellationToken);
        if (componentItem is null)
        {
            throw new InvalidOperationException("Component item was not found.");
        }

        if (!componentItem.IsComponent)
        {
            throw new InvalidOperationException("Selected component item is not marked as a component.");
        }

        var exists = await dbContext.ItemComponents.AnyAsync(
            entity => entity.ParentItemId == request.ParentItemId &&
                      entity.ComponentItemId == request.ComponentItemId &&
                      entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException("This parent/component relationship already exists.");
        }
    }

    private async Task<ItemComponentDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var itemComponent = await GetAsync(id, cancellationToken);
        return itemComponent ?? throw new InvalidOperationException("Item component was not found after save.");
    }

    private static ItemComponentDto ToDto(ItemComponent entity)
    {
        return new ItemComponentDto(
            entity.Id,
            entity.ParentItemId,
            entity.ParentItem?.Code ?? string.Empty,
            entity.ParentItem?.Name ?? string.Empty,
            entity.ComponentItemId,
            entity.ComponentItem?.Code ?? string.Empty,
            entity.ComponentItem?.Name ?? string.Empty,
            entity.Quantity,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
