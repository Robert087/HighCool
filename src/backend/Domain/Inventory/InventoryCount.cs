using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class InventoryCount : BusinessDocument
{
    public string CountNo { get; set; } = string.Empty;

    public DateTime CountDate { get; set; }

    public DateTime? SnapshotAt { get; set; }

    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public string? Notes { get; set; }

    public int Version { get; set; }

    public ICollection<InventoryCountLine> Lines { get; set; } = new List<InventoryCountLine>();
}
