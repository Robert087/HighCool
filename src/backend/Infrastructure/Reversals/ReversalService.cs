using ERP.Application.Reversals;
using ERP.Domain.Common;

namespace ERP.Infrastructure.Reversals;

public sealed class ReversalService(
    IReceiptReversalService receiptReversalService,
    IPaymentReversalService paymentReversalService,
    IShortageResolutionReversalService shortageResolutionReversalService) : IReversalService
{
    public Task<DocumentReversalDto?> ReverseAsync(
        BusinessDocumentType documentType,
        Guid documentId,
        ReverseDocumentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        return documentType switch
        {
            BusinessDocumentType.PurchaseReceipt => receiptReversalService.ReverseAsync(documentId, request, actor, cancellationToken),
            BusinessDocumentType.Payment => paymentReversalService.ReverseAsync(documentId, request, actor, cancellationToken),
            BusinessDocumentType.ShortageResolution => shortageResolutionReversalService.ReverseAsync(documentId, request, actor, cancellationToken),
            _ => throw new InvalidOperationException("Reversal is not supported for the requested document type.")
        };
    }
}
