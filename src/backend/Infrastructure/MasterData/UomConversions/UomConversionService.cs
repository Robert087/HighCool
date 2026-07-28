using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.MasterData.UomConversions;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.UomConversions;

public sealed class UomConversionService(AppDbContext dbContext) : IUomConversionService
{
    public async Task<PagedResult<UomConversionDto>> ListAsync(UomConversionListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var conversions = IncludeReferences();

        if (query.IsActive.HasValue)
        {
            conversions = conversions.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            conversions = conversions.Where(entity =>
                entity.FromUom!.Code.Contains(search) ||
                entity.FromUom.Name.Contains(search) ||
                entity.ToUom!.Code.Contains(search) ||
                entity.ToUom.Name.Contains(search));
        }

        if (query.FromUomId.HasValue)
        {
            conversions = conversions.Where(entity => entity.FromUomId == query.FromUomId.Value);
        }

        if (query.ToUomId.HasValue)
        {
            conversions = conversions.Where(entity => entity.ToUomId == query.ToUomId.Value);
        }

        conversions = ApplySorting(conversions, query);

        var totalCount = await conversions.CountAsync(cancellationToken);
        var rows = await conversions
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);

        return new PagedResult<UomConversionDto>(
            rows,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new { query.Search, query.IsActive, query.FromUomId, query.ToUomId },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public Task<UomConversionDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return IncludeReferences()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UomConversionDto> CreateAsync(
        UpsertUomConversionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await ValidateRuleAsync(request, null, cancellationToken);

        var conversion = new UomConversion
        {
            FromUomId = request.FromUomId,
            ToUomId = request.ToUomId,
            Factor = request.Factor,
            RoundingMode = request.RoundingMode,
            IsActive = request.IsActive,
            CreatedBy = actor
        };

        dbContext.UomConversions.Add(conversion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(conversion.Id, cancellationToken);
    }

    public async Task<UomConversionDto?> UpdateAsync(
        Guid id,
        UpsertUomConversionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var conversion = await dbContext.UomConversions.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (conversion is null)
        {
            return null;
        }

        await ValidateRuleAsync(request, id, cancellationToken);

        conversion.FromUomId = request.FromUomId;
        conversion.ToUomId = request.ToUomId;
        conversion.Factor = request.Factor;
        conversion.RoundingMode = request.RoundingMode;
        conversion.IsActive = request.IsActive;
        conversion.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(conversion.Id, cancellationToken);
    }

    public async Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var conversion = await dbContext.UomConversions.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
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

    private IQueryable<UomConversion> IncludeReferences()
    {
        return dbContext.UomConversions
            .AsNoTracking()
            .Include(entity => entity.FromUom)
            .Include(entity => entity.ToUom);
    }

    private async Task ValidateRuleAsync(UpsertUomConversionRequest request, Guid? currentId, CancellationToken cancellationToken)
    {
        if (request.FromUomId == request.ToUomId)
        {
            throw new InvalidOperationException("From UOM and To UOM must be different.");
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
            var activePairExists = await dbContext.UomConversions.AnyAsync(
                entity => entity.FromUomId == request.FromUomId &&
                          entity.ToUomId == request.ToUomId &&
                          entity.IsActive &&
                          entity.Id != currentId,
                cancellationToken);

            if (activePairExists)
            {
                throw new DuplicateEntityException("An active conversion already exists for this UOM pair.");
            }
        }
    }

    private async Task<UomConversionDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversion = await GetAsync(id, cancellationToken);
        return conversion ?? throw new InvalidOperationException("UOM conversion was not found after save.");
    }

    private static IQueryable<UomConversion> ApplySorting(IQueryable<UomConversion> query, UomConversionListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("toUom", true) => query.OrderBy(entity => entity.ToUom!.Code).ThenBy(entity => entity.FromUom!.Code),
            ("toUom", false) => query.OrderByDescending(entity => entity.ToUom!.Code).ThenByDescending(entity => entity.FromUom!.Code),
            ("factor", true) => query.OrderBy(entity => entity.Factor).ThenBy(entity => entity.FromUom!.Code),
            ("factor", false) => query.OrderByDescending(entity => entity.Factor).ThenByDescending(entity => entity.FromUom!.Code),
            ("createdAt", true) => query.OrderBy(entity => entity.CreatedAt).ThenBy(entity => entity.FromUom!.Code),
            ("createdAt", false) => query.OrderByDescending(entity => entity.CreatedAt).ThenByDescending(entity => entity.FromUom!.Code),
            _ when ascending => query.OrderBy(entity => entity.FromUom!.Code).ThenBy(entity => entity.ToUom!.Code),
            _ => query.OrderByDescending(entity => entity.FromUom!.Code).ThenByDescending(entity => entity.ToUom!.Code)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "toUom" => "toUom",
            "factor" => "factor",
            "createdAt" => "createdAt",
            _ => "fromUom"
        };
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static UomConversionDto ToDto(UomConversion entity)
    {
        return new UomConversionDto(
            entity.Id,
            entity.FromUomId,
            entity.FromUom?.Code ?? string.Empty,
            entity.FromUom?.Name ?? string.Empty,
            entity.ToUomId,
            entity.ToUom?.Code ?? string.Empty,
            entity.ToUom?.Name ?? string.Empty,
            entity.Factor,
            entity.RoundingMode,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
