using ERP.Application.Common.Pagination;

namespace ERP.Application.Statements;

public interface ISupplierStatementQueryService
{
    Task<PagedResult<SupplierStatementEntryDto>> ListAsync(SupplierStatementQuery query, CancellationToken cancellationToken);
}
