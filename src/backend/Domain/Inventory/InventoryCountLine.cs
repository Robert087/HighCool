using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Inventory;

public sealed class InventoryCountLine : OrganizationScopedAuditableEntity
{
    public Guid InventoryCountId { get; set; }

    public InventoryCount? InventoryCount { get; set; }

    public int LineNo { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public decimal SystemQty { get; set; }

    public decimal CountedQty { get; set; }

    public decimal VarianceQty { get; set; }

    public decimal BaseSystemQty { get; set; }

    public decimal BaseCountedQty { get; set; }

    public decimal BaseVarianceQty { get; set; }

    public string? Notes { get; set; }
}
