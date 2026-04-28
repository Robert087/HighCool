namespace ERP.Domain.Inventory;

public enum SourceDocumentType
{
    PurchaseReceipt = 1,
    PurchaseReceiptReversal = 2,
    ShortageResolution = 3,
    PurchaseReturn = 4,
    DocumentReversal = 5
}
