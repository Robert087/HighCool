namespace ERP.Application.Purchasing.PurchaseOrders;

public sealed record PurchaseOrderLineDto(
    Guid Id,
    int LineNo,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal OrderedQty,
    decimal UnitPrice,
    Guid UomId,
    string UomCode,
    string UomName,
    decimal ReceivedQty,
    decimal RemainingQty,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
