using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory.Transfers;

public interface IInventoryTransferService
{
    Task<PagedResult<InventoryTransferListItemDto>> ListAsync(InventoryTransferListQuery query, CancellationToken cancellationToken);

    Task<InventoryTransferDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<InventoryTransferDto> CreateDraftAsync(UpsertInventoryTransferRequest request, string actor, CancellationToken cancellationToken);

    Task<InventoryTransferDto?> UpdateDraftAsync(Guid id, UpsertInventoryTransferRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken);
}
