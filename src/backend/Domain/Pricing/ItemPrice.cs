using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Pricing;

public sealed class ItemPrice : OrganizationScopedAuditableEntity
{
    public Guid PriceListId { get; set; }

    public PriceList? PriceList { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public decimal MinimumQuantity { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public int Version { get; set; }
}
