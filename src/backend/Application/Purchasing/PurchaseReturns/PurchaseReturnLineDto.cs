namespace ERP.Application.Purchasing.PurchaseReturns;

public sealed record PurchaseReturnLineDto(
    Guid Id,
    int LineNo,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid? ComponentId,
    string? ComponentCode,
    string? ComponentName,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    decimal ReturnQty,
    decimal RemainingReturnableQty,
    Guid UomId,
    string UomCode,
    string UomName,
    decimal BaseQty,
    Guid? ReferenceReceiptLineId,
    int? ReferenceReceiptLineNo,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
