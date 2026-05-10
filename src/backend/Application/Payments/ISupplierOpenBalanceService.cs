using ERP.Application.Common.Pagination;

namespace ERP.Application.Payments;

public interface ISupplierOpenBalanceService
{
    Task<PagedResult<SupplierOpenBalanceDto>> ListAsync(SupplierOpenBalanceQuery query, CancellationToken cancellationToken);
}
