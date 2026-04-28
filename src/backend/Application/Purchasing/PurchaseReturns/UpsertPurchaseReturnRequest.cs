namespace ERP.Application.Purchasing.PurchaseReturns;

public sealed record UpsertPurchaseReturnRequest(
    string? ReturnNo,
    Guid SupplierId,
    Guid? ReferenceReceiptId,
    DateTime? ReturnDate,
    string? Notes,
    IReadOnlyList<UpsertPurchaseReturnLineRequest> Lines);
