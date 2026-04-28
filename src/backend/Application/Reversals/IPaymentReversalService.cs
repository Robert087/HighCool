namespace ERP.Application.Reversals;

public interface IPaymentReversalService
{
    Task<DocumentReversalDto?> ReverseAsync(Guid paymentId, ReverseDocumentRequest request, string actor, CancellationToken cancellationToken);
}
