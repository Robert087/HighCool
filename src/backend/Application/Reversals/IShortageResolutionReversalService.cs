namespace ERP.Application.Reversals;

public interface IShortageResolutionReversalService
{
    Task<DocumentReversalDto?> ReverseAsync(Guid resolutionId, ReverseDocumentRequest request, string actor, CancellationToken cancellationToken);
}
