using ERP.Application.Common.Pagination;

namespace ERP.Application.Purchasing.PurchaseOrders;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderListItemDto>> ListAsync(PurchaseOrderListQuery query, CancellationToken cancellationToken);

    Task<PurchaseOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseOrderDto> CreateDraftAsync(UpsertPurchaseOrderRequest request, string actor, CancellationToken cancellationToken);

    Task<PurchaseOrderDto?> UpdateDraftAsync(Guid id, UpsertPurchaseOrderRequest request, string actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<PurchaseOrderAvailableLineDto>> ListAvailableLinesForReceiptAsync(Guid id, CancellationToken cancellationToken);
}
