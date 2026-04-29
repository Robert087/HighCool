using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Purchasing;

public sealed class PurchaseReceiptLine : OrganizationScopedAuditableEntity
{
    public Guid PurchaseReceiptId { get; set; }

    public PurchaseReceipt? PurchaseReceipt { get; set; }

    public int LineNo { get; set; }

    public Guid? PurchaseOrderLineId { get; set; }

    public PurchaseOrderLine? PurchaseOrderLine { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public decimal? OrderedQtySnapshot { get; set; }

    public decimal ReceivedQty { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public string? Notes { get; set; }

    public ICollection<PurchaseReceiptLineComponent> Components { get; set; } = new List<PurchaseReceiptLineComponent>();
}
