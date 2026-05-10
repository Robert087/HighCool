using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Purchasing;

public sealed class PurchaseReturnLine : OrganizationScopedAuditableEntity
{
    public Guid PurchaseReturnId { get; set; }

    public PurchaseReturn? PurchaseReturn { get; set; }

    public int LineNo { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid? ComponentId { get; set; }

    public Item? Component { get; set; }

    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public decimal ReturnQty { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public decimal BaseQty { get; set; }

    public Guid? ReferenceReceiptLineId { get; set; }

    public PurchaseReceiptLine? ReferenceReceiptLine { get; set; }
}
