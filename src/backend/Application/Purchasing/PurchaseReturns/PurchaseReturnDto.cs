using ERP.Domain.Common;

namespace ERP.Application.Purchasing.PurchaseReturns;

public sealed record PurchaseReturnDto(
    Guid Id,
    string ReturnNo,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid? ReferenceReceiptId,
    string? ReferenceReceiptNo,
    DateTime ReturnDate,
    string? Notes,
    DocumentStatus Status,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    IReadOnlyList<PurchaseReturnLineDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
