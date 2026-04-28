namespace ERP.Application.Reversals;

public interface IReceiptReversalService
{
    Task<DocumentReversalDto?> ReverseAsync(Guid receiptId, ReverseDocumentRequest request, string actor, CancellationToken cancellationToken);
}
