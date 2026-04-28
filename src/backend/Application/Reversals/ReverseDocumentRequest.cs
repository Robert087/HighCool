namespace ERP.Application.Reversals;

public sealed record ReverseDocumentRequest(
    DateTime? ReversalDate,
    string? ReversalReason);
