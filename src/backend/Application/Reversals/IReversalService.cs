using ERP.Domain.Common;

namespace ERP.Application.Reversals;

public interface IReversalService
{
    Task<DocumentReversalDto?> ReverseAsync(
        BusinessDocumentType documentType,
        Guid documentId,
        ReverseDocumentRequest request,
        string actor,
        CancellationToken cancellationToken);
}
