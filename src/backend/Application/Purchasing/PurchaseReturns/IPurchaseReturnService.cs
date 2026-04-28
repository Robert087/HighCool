using ERP.Application.Common.Pagination;

namespace ERP.Application.Purchasing.PurchaseReturns;

public interface IPurchaseReturnService
{
    Task<PagedResult<PurchaseReturnListItemDto>> ListAsync(PurchaseReturnListQuery query, CancellationToken cancellationToken);

    Task<PurchaseReturnDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseReturnDto> CreateDraftAsync(UpsertPurchaseReturnRequest request, string actor, CancellationToken cancellationToken);

    Task<PurchaseReturnDto?> UpdateDraftAsync(Guid id, UpsertPurchaseReturnRequest request, string actor, CancellationToken cancellationToken);
}
