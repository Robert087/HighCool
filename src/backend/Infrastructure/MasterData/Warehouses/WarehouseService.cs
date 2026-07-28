using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.MasterData.Warehouses;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Warehouses;

public sealed class WarehouseService(AppDbContext dbContext) : IWarehouseService
{
    public async Task<PagedResult<WarehouseDto>> ListAsync(
        WarehouseListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var warehouses = dbContext.Warehouses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            warehouses = warehouses.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search) ||
                (entity.Location != null && entity.Location.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            warehouses = warehouses.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        warehouses = ApplySorting(warehouses, query);

        var totalCount = await warehouses.CountAsync(cancellationToken);
        var rows = await warehouses
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);

        return new PagedResult<WarehouseDto>(
            rows,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new { query.Search, query.IsActive },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public Task<WarehouseDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Warehouses
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<WarehouseDto> CreateAsync(
        UpsertWarehouseRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await EnsureCodeIsUniqueAsync(request.Code, null, cancellationToken);

        var warehouse = new Warehouse
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Location = NormalizeOptional(request.Location),
            IsActive = request.IsActive,
            CreatedBy = actor
        };

        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(warehouse);
    }

    public async Task<WarehouseDto?> UpdateAsync(
        Guid id,
        UpsertWarehouseRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (warehouse is null)
        {
            return null;
        }

        await EnsureCodeIsUniqueAsync(request.Code, id, cancellationToken);

        warehouse.Code = request.Code.Trim();
        warehouse.Name = request.Name.Trim();
        warehouse.Location = NormalizeOptional(request.Location);
        warehouse.IsActive = request.IsActive;
        warehouse.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(warehouse);
    }

    public async Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (warehouse is null)
        {
            return false;
        }

        if (!warehouse.IsActive)
        {
            return true;
        }

        warehouse.IsActive = false;
        warehouse.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();

        var exists = await dbContext.Warehouses.AnyAsync(
            entity => entity.Code == normalizedCode && entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException($"Warehouse code '{normalizedCode}' already exists.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IQueryable<Warehouse> ApplySorting(IQueryable<Warehouse> query, WarehouseListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("code", true) => query.OrderBy(entity => entity.Code).ThenBy(entity => entity.Name),
            ("code", false) => query.OrderByDescending(entity => entity.Code).ThenByDescending(entity => entity.Name),
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
            "createdAt" => "createdAt",
            _ => "name"
        };
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static WarehouseDto ToDto(Warehouse entity)
    {
        return new WarehouseDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Location,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
