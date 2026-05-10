using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class StockLedgerEntry : OrganizationScopedAuditableEntity
{
    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public StockTransactionType TransactionType { get; set; } = StockTransactionType.PurchaseReceipt;

    public SourceDocumentType SourceDocType { get; set; } = SourceDocumentType.PurchaseReceipt;

    public Guid SourceDocId { get; set; }

    public Guid? SourceLineId { get; set; }

    public decimal QtyIn { get; set; }

    public decimal QtyOut { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public decimal BaseQty { get; set; }

    public decimal RunningBalanceQty { get; set; }

    public DateTime TransactionDate { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal? TotalCost { get; set; }
}
