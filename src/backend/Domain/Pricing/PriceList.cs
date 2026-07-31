using ERP.Domain.Common;

namespace ERP.Domain.Pricing;

public sealed class PriceList : OrganizationScopedAuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public PriceListType Type { get; set; }

    public string Currency { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    public int Version { get; set; }

    public ICollection<ItemPrice> ItemPrices { get; set; } = new List<ItemPrice>();
}
