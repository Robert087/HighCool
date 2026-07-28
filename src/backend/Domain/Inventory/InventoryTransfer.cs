using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class InventoryTransfer : BusinessDocument
{
    public string TransferNo { get; set; } = string.Empty;

    public DateTime TransferDate { get; set; }

    public Guid SourceWarehouseId { get; set; }

    public Warehouse? SourceWarehouse { get; set; }

    public Guid DestinationWarehouseId { get; set; }

    public Warehouse? DestinationWarehouse { get; set; }

    public string? Notes { get; set; }

    public int Version { get; set; }

    public ICollection<InventoryTransferLine> Lines { get; set; } = new List<InventoryTransferLine>();
}
