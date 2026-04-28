namespace ERP.Domain.Statements;

public enum SupplierStatementEffectType
{
    PurchaseReceipt = 1,
    ShortageFinancialResolution = 2,
    Payment = 3,
    PurchaseReturn = 4,
    PurchaseReceiptReversal = 5,
    PaymentReversal = 6,
    ShortageResolutionReversal = 7
}
