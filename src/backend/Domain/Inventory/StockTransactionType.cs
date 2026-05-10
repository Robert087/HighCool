namespace ERP.Domain.Inventory;

public enum StockTransactionType
{
    PurchaseReceipt = 1,
    PurchaseReceiptReversal = 2,
    ShortagePhysicalResolution = 3,
    PurchaseReturn = 4,
    ShortageResolutionReversal = 5
}
