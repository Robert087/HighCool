namespace ERP.Application.Purchasing.PurchaseReceipts;

public sealed record UpsertPurchaseReceiptDraftRequest(
    string? ReceiptNo,
    Guid SupplierId,
    Guid WarehouseId,
    Guid? PurchaseOrderId,
    DateTime? ReceiptDate,
    decimal SupplierPayableAmount,
    string? Notes,
    IReadOnlyList<UpsertPurchaseReceiptLineRequest> Lines);
