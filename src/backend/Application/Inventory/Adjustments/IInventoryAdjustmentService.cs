using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory.Adjustments;

public interface IInventoryAdjustmentService
{
    Task<PagedResult<InventoryAdjustmentListItemDto>> ListAsync(InventoryAdjustmentListQuery query, CancellationToken cancellationToken);

    Task<InventoryAdjustmentDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<InventoryAdjustmentDto> CreateDraftAsync(UpsertInventoryAdjustmentRequest request, string actor, CancellationToken cancellationToken);

    Task<InventoryAdjustmentDto?> UpdateDraftAsync(Guid id, UpsertInventoryAdjustmentRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken);
}
