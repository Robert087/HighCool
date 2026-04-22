using ERP.Domain.Purchasing;
using ERP.Domain.Payments;
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
}
