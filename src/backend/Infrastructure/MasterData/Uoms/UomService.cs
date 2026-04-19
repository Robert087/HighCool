using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.Uoms;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Uoms;

public sealed class UomService(AppDbContext dbContext) : IUomService
{
    public async Task<IReadOnlyList<UomDto>> ListAsync(UomListQuery query, CancellationToken cancellationToken)
    {
        var uoms = dbContext.Uoms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            uoms = uoms.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search));
        }

        if (query.IsActive.HasValue)
        {
            uoms = uoms.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        return await uoms
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.Code)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);
    }

    public Task<UomDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Uoms
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UomDto> CreateAsync(
        UpsertUomRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await EnsureCodeIsUniqueAsync(request.Code, null, cancellationToken);

        var uom = new Uom
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Precision = request.Precision,
            AllowsFraction = request.AllowsFraction,
            IsActive = request.IsActive,
            CreatedBy = actor
        };

        dbContext.Uoms.Add(uom);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(uom);
    }

    public async Task<UomDto?> UpdateAsync(
        Guid id,
        UpsertUomRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var uom = await dbContext.Uoms.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (uom is null)
        {
            return null;
        }

        await EnsureCodeIsUniqueAsync(request.Code, id, cancellationToken);

        uom.Code = request.Code.Trim();
        uom.Name = request.Name.Trim();
        uom.Precision = request.Precision;
        uom.AllowsFraction = request.AllowsFraction;
        uom.IsActive = request.IsActive;
        uom.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(uom);
    }

    public async Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var uom = await dbContext.Uoms.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (uom is null)
        {
            return false;
        }

        if (!uom.IsActive)
        {
            return true;
        }

        uom.IsActive = false;
        uom.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();

        var exists = await dbContext.Uoms.AnyAsync(
            entity => entity.Code == normalizedCode && entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException($"UOM code '{normalizedCode}' already exists.");
        }
    }

    private static UomDto ToDto(Uom entity)
    {
        return new UomDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Precision,
            entity.AllowsFraction,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
