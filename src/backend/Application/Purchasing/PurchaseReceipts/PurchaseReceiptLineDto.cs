namespace ERP.Application.Purchasing.PurchaseReceipts;

public sealed record PurchaseReceiptLineDto(
    Guid Id,
    int LineNo,
    Guid? PurchaseOrderLineId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal? OrderedQtySnapshot,
    decimal ReceivedQty,
    decimal ReturnedQty,
    decimal RemainingReturnableQty,
    Guid UomId,
    string UomCode,
    string UomName,
    string? Notes,
    IReadOnlyList<PurchaseReceiptLineComponentDto> Components,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
