using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.MasterData.ItemCategories;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.ItemCategories;

public sealed class ItemCategoryService(AppDbContext dbContext) : IItemCategoryService
{
    public async Task<PagedResult<ItemCategoryDto>> ListAsync(ItemCategoryListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var categories = dbContext.ItemCategories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            categories = categories.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search) ||
                (entity.Description != null && entity.Description.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            categories = categories.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        categories = ApplySorting(categories, query);

        var totalCount = await categories.CountAsync(cancellationToken);
        var rows = await categories
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);

        return new PagedResult<ItemCategoryDto>(
            rows,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new { query.Search, query.IsActive },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public Task<ItemCategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.ItemCategories
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ItemCategoryDto> CreateAsync(UpsertItemCategoryRequest request, string actor, CancellationToken cancellationToken)
    {
        await EnsureCodeIsUniqueAsync(request.Code, null, cancellationToken);

        var category = new ItemCategory
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            IsActive = request.IsActive,
            CreatedBy = actor
        };

        dbContext.ItemCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task<ItemCategoryDto?> UpdateAsync(Guid id, UpsertItemCategoryRequest request, string actor, CancellationToken cancellationToken)
    {
        var category = await dbContext.ItemCategories.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        await EnsureCodeIsUniqueAsync(request.Code, id, cancellationToken);

        category.Code = request.Code.Trim();
        category.Name = request.Name.Trim();
        category.Description = NormalizeOptional(request.Description);
        category.IsActive = request.IsActive;
        category.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public Task<bool> ActivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        return SetStatusAsync(id, true, actor, cancellationToken);
    }

    public Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        return SetStatusAsync(id, false, actor, cancellationToken);
    }

    private async Task<bool> SetStatusAsync(Guid id, bool isActive, string actor, CancellationToken cancellationToken)
    {
        var category = await dbContext.ItemCategories.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (category is null)
        {
            return false;
        }

        if (category.IsActive == isActive)
        {
            return true;
        }

        category.IsActive = isActive;
        category.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();
        var exists = await dbContext.ItemCategories.AnyAsync(
            entity => entity.Code == normalizedCode && entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException($"Item category code '{normalizedCode}' already exists.");
        }
    }

    private static IQueryable<ItemCategory> ApplySorting(IQueryable<ItemCategory> query, ItemCategoryListQuery request)
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static ItemCategoryDto ToDto(ItemCategory entity)
    {
        return new ItemCategoryDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
