using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public interface IPaymentAllocationService
{
    Task ValidateDraftAsync(Payment payment, CancellationToken cancellationToken);

    Task ValidateForPostingAsync(Payment payment, CancellationToken cancellationToken);
}
