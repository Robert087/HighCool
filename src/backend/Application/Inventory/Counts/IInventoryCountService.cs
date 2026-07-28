using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory.Counts;

public interface IInventoryCountService
{
    Task<PagedResult<InventoryCountListItemDto>> ListAsync(
        InventoryCountListQuery query,
        CancellationToken cancellationToken);

    Task<InventoryCountDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<InventoryCountDto> CreateDraftAsync(
        UpsertInventoryCountRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<InventoryCountDto?> UpdateDraftAsync(
        Guid id,
        UpsertInventoryCountRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken);

    Task<InventoryCountDto?> RefreshSystemQuantitiesAsync(
        Guid id,
        string actor,
        CancellationToken cancellationToken);
}
