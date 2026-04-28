using ERP.Domain.Common;

namespace ERP.Application.Purchasing.PurchaseReceipts;

public sealed record PurchaseReceiptListItemDto(
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
    DocumentStatus Status,
    int LineCount,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
