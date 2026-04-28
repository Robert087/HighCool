using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory;

public interface IStockBalanceService
{
    Task<PagedResult<StockBalanceDto>> ListAsync(StockBalanceQuery query, CancellationToken cancellationToken);
}
