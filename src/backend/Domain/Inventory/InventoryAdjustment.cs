using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class InventoryAdjustment : BusinessDocument
{
    public string AdjustmentNo { get; set; } = string.Empty;

    public DateTime AdjustmentDate { get; set; }

    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int Version { get; set; }

    public ICollection<InventoryAdjustmentLine> Lines { get; set; } = new List<InventoryAdjustmentLine>();
}
