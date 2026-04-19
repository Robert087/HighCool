using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class Item : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid BaseUomId { get; set; }

    public Uom? BaseUom { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSellable { get; set; }

    public bool IsComponent { get; set; }
}
