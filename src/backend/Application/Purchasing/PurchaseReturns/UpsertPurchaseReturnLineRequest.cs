namespace ERP.Application.Purchasing.PurchaseReturns;

public sealed record UpsertPurchaseReturnLineRequest(
    int LineNo,
    Guid ItemId,
    Guid? ComponentId,
    Guid WarehouseId,
    decimal ReturnQty,
    Guid UomId,
    Guid? ReferenceReceiptLineId);
