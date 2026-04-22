using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Purchasing;

public sealed class PurchaseReceipt : BusinessDocument
{
    public string ReceiptNo { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    public DateTime ReceiptDate { get; set; }

    public decimal SupplierPayableAmount { get; set; }

    public string? Notes { get; set; }

    public ICollection<PurchaseReceiptLine> Lines { get; set; } = new List<PurchaseReceiptLine>();
}
