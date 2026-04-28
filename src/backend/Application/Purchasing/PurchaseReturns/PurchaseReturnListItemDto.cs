using ERP.Domain.Common;

namespace ERP.Application.Purchasing.PurchaseReturns;

public sealed record PurchaseReturnListItemDto(
    Guid Id,
    string ReturnNo,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid? ReferenceReceiptId,
    string? ReferenceReceiptNo,
    DateTime ReturnDate,
    DocumentStatus Status,
    int LineCount,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
