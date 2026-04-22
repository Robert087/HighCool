namespace ERP.Application.Statements;

public interface ISupplierBalanceService
{
    Task<SupplierStatementSummaryDto?> GetSummaryAsync(SupplierStatementSummaryQuery query, CancellationToken cancellationToken);
}
