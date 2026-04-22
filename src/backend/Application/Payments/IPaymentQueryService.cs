namespace ERP.Application.Payments;

public interface IPaymentQueryService
{
    Task<IReadOnlyList<PaymentListItemDto>> ListAsync(PaymentListQuery query, CancellationToken cancellationToken);

    Task<PaymentDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentAllocationDto>> GetAllocationsAsync(Guid id, CancellationToken cancellationToken);
}
