using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class InventoryIssue : BusinessDocument
{
    public string IssueNo { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; }

    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public InventoryIssueReason Reason { get; set; } = InventoryIssueReason.InternalConsumption;

    public string? ReferenceNo { get; set; }

    public string? RequestedBy { get; set; }

    public string? Notes { get; set; }

    public int Version { get; set; }

    public ICollection<InventoryIssueLine> Lines { get; set; } = new List<InventoryIssueLine>();
}
