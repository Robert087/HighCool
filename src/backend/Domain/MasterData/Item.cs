using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class Item : OrganizationScopedAuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

    public ItemCategory? Category { get; set; }

    public Guid BaseUomId { get; set; }

    public Uom? BaseUom { get; set; }

    public Guid? DefaultWarehouseId { get; set; }

    public Warehouse? DefaultWarehouse { get; set; }

    public decimal MinimumStockQuantity { get; set; }

    public ICollection<ItemComponent> Components { get; set; } = new List<ItemComponent>();

    public bool IsActive { get; set; } = true;

    public bool IsSellable { get; set; }

    public bool HasComponents { get; set; }
}
