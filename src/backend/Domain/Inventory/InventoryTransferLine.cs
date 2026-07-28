using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class InventoryTransferLine : OrganizationScopedAuditableEntity
{
    public Guid InventoryTransferId { get; set; }

    public InventoryTransfer? InventoryTransfer { get; set; }

    public int LineNo { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public decimal Quantity { get; set; }

    public decimal BaseQty { get; set; }

    public string? Notes { get; set; }
}
