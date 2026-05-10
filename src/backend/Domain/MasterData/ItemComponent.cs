using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class ItemComponent : OrganizationScopedAuditableEntity
{
    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid ComponentItemId { get; set; }

    public Item? ComponentItem { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public decimal Quantity { get; set; }
}
