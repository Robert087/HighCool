using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class UomConversion : OrganizationScopedAuditableEntity
{
    public Guid FromUomId { get; set; }

    public Uom? FromUom { get; set; }

    public Guid ToUomId { get; set; }

    public Uom? ToUom { get; set; }

    public decimal Factor { get; set; }

    public RoundingMode RoundingMode { get; set; } = RoundingMode.None;

    public bool IsActive { get; set; } = true;
}
