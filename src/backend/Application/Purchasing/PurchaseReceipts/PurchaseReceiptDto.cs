using ERP.Domain.Common;

namespace ERP.Application.Purchasing.PurchaseReceipts;

public sealed record PurchaseReceiptDto(
    Guid Id,
    string ReceiptNo,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNo,
    DateTime ReceiptDate,
    decimal SupplierPayableAmount,
    string? Notes,
    DocumentStatus Status,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    IReadOnlyList<PurchaseReceiptLineDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
