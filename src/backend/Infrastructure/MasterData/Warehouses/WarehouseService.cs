using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.Warehouses;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Warehouses;

public sealed class WarehouseService(AppDbContext dbContext) : IWarehouseService
{
    public async Task<IReadOnlyList<WarehouseDto>> ListAsync(
        WarehouseListQuery query,
        CancellationToken cancellationToken)
    {
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

        return await warehouses
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.Code)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);
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
