using ERP.Application.Common.Pagination;

namespace ERP.Application.MasterData.ItemCategories;

public interface IItemCategoryService
{
    Task<PagedResult<ItemCategoryDto>> ListAsync(ItemCategoryListQuery query, CancellationToken cancellationToken);

    Task<ItemCategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ItemCategoryDto> CreateAsync(UpsertItemCategoryRequest request, string actor, CancellationToken cancellationToken);

    Task<ItemCategoryDto?> UpdateAsync(Guid id, UpsertItemCategoryRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> ActivateAsync(Guid id, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
