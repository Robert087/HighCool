namespace ERP.Application.Payments;

public interface IPaymentService
{
    Task<PaymentDto> CreateDraftAsync(UpsertPaymentRequest request, string actor, CancellationToken cancellationToken);

    Task<PaymentDto?> UpdateDraftAsync(Guid id, UpsertPaymentRequest request, string actor, CancellationToken cancellationToken);
}
