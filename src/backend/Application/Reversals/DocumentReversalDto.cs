using ERP.Domain.Common;

namespace ERP.Application.Reversals;

public sealed record DocumentReversalDto(
    Guid Id,
    string ReversalNo,
    BusinessDocumentType ReversedDocumentType,
    Guid ReversedDocumentId,
    DateTime ReversalDate,
    string ReversalReason,
    DateTime CreatedAt,
    string CreatedBy);
