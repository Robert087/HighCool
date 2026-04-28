namespace ERP.Domain.Statements;

public enum SupplierStatementSourceDocumentType
{
    PurchaseReceipt = 1,
    ShortageResolution = 2,
    Payment = 3,
    PurchaseReturn = 4,
    DocumentReversal = 5,
    ShortageFinancialResolution = 6,
    PurchaseReceiptReversal = 7,
    PaymentReversal = 8,
    ShortageResolutionReversal = 9
}
