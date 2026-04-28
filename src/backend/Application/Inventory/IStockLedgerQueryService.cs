using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory;

public interface IStockLedgerQueryService
{
    Task<PagedResult<StockLedgerEntryDto>> ListAsync(StockLedgerQuery query, CancellationToken cancellationToken);
}
