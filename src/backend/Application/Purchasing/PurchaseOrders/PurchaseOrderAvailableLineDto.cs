namespace ERP.Application.Purchasing.PurchaseOrders;

public sealed record PurchaseOrderAvailableLineDto(
    Guid PurchaseOrderLineId,
    int LineNo,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal OrderedQty,
    decimal UnitPrice,
    decimal ReceivedQty,
    decimal RemainingQty,
    Guid UomId,
    string UomCode,
    string UomName,
    string? Notes);
