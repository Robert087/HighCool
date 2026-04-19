using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class ItemComponent : AuditableEntity
{
    public Guid ParentItemId { get; set; }

    public Item? ParentItem { get; set; }

    public Guid ComponentItemId { get; set; }

    public Item? ComponentItem { get; set; }

    public decimal Quantity { get; set; }
}
