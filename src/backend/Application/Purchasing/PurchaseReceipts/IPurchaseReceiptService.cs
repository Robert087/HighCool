using ERP.Application.Common.Pagination;

namespace ERP.Application.Purchasing.PurchaseReceipts;

public interface IPurchaseReceiptService
{
    Task<PagedResult<PurchaseReceiptListItemDto>> ListAsync(PurchaseReceiptListQuery query, CancellationToken cancellationToken);

    Task<PurchaseReceiptDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseReceiptDto> CreateDraftAsync(UpsertPurchaseReceiptDraftRequest request, string actor, CancellationToken cancellationToken);

    Task<PurchaseReceiptDto?> UpdateDraftAsync(Guid id, UpsertPurchaseReceiptDraftRequest request, string actor, CancellationToken cancellationToken);
}
