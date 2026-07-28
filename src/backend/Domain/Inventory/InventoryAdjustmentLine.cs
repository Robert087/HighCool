using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class InventoryAdjustmentLine : OrganizationScopedAuditableEntity
{
    public Guid InventoryAdjustmentId { get; set; }

    public InventoryAdjustment? InventoryAdjustment { get; set; }

    public int LineNo { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public decimal Quantity { get; set; }

    public InventoryAdjustmentType AdjustmentType { get; set; } = InventoryAdjustmentType.Increase;

    public decimal BaseQty { get; set; }

    public string? Notes { get; set; }
}
