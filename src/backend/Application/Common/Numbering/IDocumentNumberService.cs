namespace ERP.Application.Common.Numbering;

public interface IDocumentNumberService
{
    Task<string> GenerateAsync(
        DocumentNumberRequest request,
        string actor,
        CancellationToken cancellationToken);
}
