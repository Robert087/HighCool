using ERP.Domain.Purchasing;
using ERP.Domain.Payments;
using ERP.Domain.Reversals;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;

namespace ERP.Application.Statements;

public interface ISupplierStatementPostingService
{
    Task<IReadOnlyList<SupplierStatementEntry>> CreatePurchaseReceiptEntriesAsync(
        PurchaseReceipt receipt,
        IReadOnlyList<ERP.Domain.Inventory.StockLedgerEntry> stockEntries,
        string actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierStatementEntry>> CreateFinancialShortageResolutionEntriesAsync(
        ShortageResolution resolution,
        string actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierStatementEntry>> CreatePaymentEntriesAsync(
        Payment payment,
        string actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierStatementEntry>> CreatePurchaseReturnEntriesAsync(
        PurchaseReturn purchaseReturn,
        decimal returnAmount,
        string actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierStatementEntry>> CreatePurchaseReceiptReversalEntriesAsync(
        PurchaseReceipt receipt,
        DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierStatementEntry>> CreatePaymentReversalEntriesAsync(
        Payment payment,
        DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierStatementEntry>> CreateShortageResolutionReversalEntriesAsync(
        ShortageResolution resolution,
        DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken);
}
