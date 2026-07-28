namespace ERP.Domain.Inventory;

public enum StockTransactionType
{
    PurchaseReceipt = 1,
    PurchaseReceiptReversal = 2,
    ShortagePhysicalResolution = 3,
    PurchaseReturn = 4,
    ShortageResolutionReversal = 5,
    InventoryAdjustmentIncrease = 6,
    InventoryAdjustmentDecrease = 7,
    InventoryAdjustmentCancellation = 8,
    InventoryTransferOut = 9,
    InventoryTransferIn = 10,
    InventoryTransferCancellationIn = 11,
    InventoryTransferCancellationOut = 12,
    InventoryCountIncrease = 13,
    InventoryCountDecrease = 14,
    InventoryCountCancellationOut = 15,
    InventoryCountCancellationIn = 16
}
