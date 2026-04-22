namespace ERP.Application.Payments;

public interface ISupplierOpenBalanceService
{
    Task<IReadOnlyList<SupplierOpenBalanceDto>> ListAsync(SupplierOpenBalanceQuery query, CancellationToken cancellationToken);
}
