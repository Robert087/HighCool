namespace ERP.Application.Statements;

public interface ISupplierStatementQueryService
{
    Task<IReadOnlyList<SupplierStatementEntryDto>> ListAsync(SupplierStatementQuery query, CancellationToken cancellationToken);
}
