using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class ItemUomConversion : AuditableEntity
{
    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid FromUomId { get; set; }

    public Uom? FromUom { get; set; }

    public Guid ToUomId { get; set; }

    public Uom? ToUom { get; set; }

    public decimal Factor { get; set; }

    public RoundingMode RoundingMode { get; set; } = RoundingMode.None;

    public decimal MinFraction { get; set; }

    public bool IsActive { get; set; } = true;
}
