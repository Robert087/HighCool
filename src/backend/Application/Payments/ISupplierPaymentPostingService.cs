namespace ERP.Application.Payments;

public interface ISupplierPaymentPostingService
{
    Task<PaymentDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken);
}
