namespace ERP.Domain.Inventory;

public enum SourceDocumentType
{
    PurchaseReceipt = 1,
    PurchaseReceiptReversal = 2,
    ShortageResolution = 3,
    PurchaseReturn = 4,
    DocumentReversal = 5,
    InventoryAdjustment = 6,
    InventoryAdjustmentCancellation = 7,
    InventoryTransfer = 8,
    InventoryTransferCancellation = 9
}
