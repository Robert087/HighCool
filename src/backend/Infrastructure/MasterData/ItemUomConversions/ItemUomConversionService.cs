using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.ItemUomConversions;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.ItemUomConversions;

public sealed class ItemUomConversionService(AppDbContext dbContext) : IItemUomConversionService
{
    public async Task<IReadOnlyList<ItemUomConversionDto>> ListAsync(ItemUomConversionListQuery query, CancellationToken cancellationToken)
    {
        var conversions = IncludeReferences();

        if (query.ItemId.HasValue)
        {
            conversions = conversions.Where(entity => entity.ItemId == query.ItemId.Value);
        }

        if (query.IsActive.HasValue)
        {
            conversions = conversions.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            conversions = conversions.Where(entity =>
                entity.Item!.Code.Contains(search) ||
                entity.Item.Name.Contains(search) ||
                entity.FromUom!.Code.Contains(search) ||
                entity.ToUom!.Code.Contains(search));
        }

        return await conversions
            .OrderBy(entity => entity.Item!.Name)
            .ThenBy(entity => entity.FromUom!.Code)
            .ThenBy(entity => entity.ToUom!.Code)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);
    }

    public Task<ItemUomConversionDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return IncludeReferences()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ItemUomConversionDto> CreateAsync(
        UpsertItemUomConversionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await ValidateRuleAsync(request, null, cancellationToken);

        var conversion = new ItemUomConversion
        {
            ItemId = request.ItemId,
            FromUomId = request.FromUomId,
            ToUomId = request.ToUomId,
            Factor = request.Factor,
            RoundingMode = request.RoundingMode,
            MinFraction = request.MinFraction,
            IsActive = request.IsActive,
            CreatedBy = actor
        };

        dbContext.ItemUomConversions.Add(conversion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(conversion.Id, cancellationToken);
    }

    public async Task<ItemUomConversionDto?> UpdateAsync(
        Guid id,
        UpsertItemUomConversionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var conversion = await dbContext.ItemUomConversions.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (conversion is null)
        {
            return null;
        }

        await ValidateRuleAsync(request, id, cancellationToken);

        conversion.ItemId = request.ItemId;
        conversion.FromUomId = request.FromUomId;
        conversion.ToUomId = request.ToUomId;
        conversion.Factor = request.Factor;
        conversion.RoundingMode = request.RoundingMode;
        conversion.MinFraction = request.MinFraction;
        conversion.IsActive = request.IsActive;
        conversion.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(conversion.Id, cancellationToken);
    }

    public async Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var conversion = await dbContext.ItemUomConversions.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (conversion is null)
        {
            return false;
        }

        if (!conversion.IsActive)
        {
            return true;
        }

        conversion.IsActive = false;
        conversion.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<ItemUomConversion> IncludeReferences()
    {
        return dbContext.ItemUomConversions
            .AsNoTracking()
            .Include(entity => entity.Item)
            .Include(entity => entity.FromUom)
            .Include(entity => entity.ToUom);
    }

    private async Task ValidateRuleAsync(UpsertItemUomConversionRequest request, Guid? currentId, CancellationToken cancellationToken)
    {
        if (request.FromUomId == request.ToUomId)
        {
            throw new InvalidOperationException("From UOM and To UOM must be different.");
        }

        var item = await dbContext.Items.SingleOrDefaultAsync(entity => entity.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            throw new InvalidOperationException("Item was not found.");
        }

        var uomsExist = await dbContext.Uoms.CountAsync(
            entity => entity.Id == request.FromUomId || entity.Id == request.ToUomId,
            cancellationToken);

        if (uomsExist != 2)
        {
            throw new InvalidOperationException("One or more UOM references were not found.");
        }

        if (request.IsActive)
        {
            var activePairExists = await dbContext.ItemUomConversions.AnyAsync(
                entity => entity.ItemId == request.ItemId &&
                          entity.FromUomId == request.FromUomId &&
                          entity.ToUomId == request.ToUomId &&
                          entity.IsActive &&
                          entity.Id != currentId,
                cancellationToken);

            if (activePairExists)
            {
                throw new DuplicateEntityException("An active conversion already exists for this item and UOM pair.");
            }
        }
    }

    private async Task<ItemUomConversionDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversion = await GetAsync(id, cancellationToken);
        return conversion ?? throw new InvalidOperationException("Item UOM conversion was not found after save.");
    }

    private static ItemUomConversionDto ToDto(ItemUomConversion entity)
    {
        return new ItemUomConversionDto(
            entity.Id,
            entity.ItemId,
            entity.Item?.Code ?? string.Empty,
            entity.Item?.Name ?? string.Empty,
            entity.FromUomId,
            entity.FromUom?.Code ?? string.Empty,
            entity.ToUomId,
            entity.ToUom?.Code ?? string.Empty,
            entity.Factor,
            entity.RoundingMode,
            entity.MinFraction,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
