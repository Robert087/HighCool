using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class Uom : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Precision { get; set; }

    public bool AllowsFraction { get; set; }

    public bool IsActive { get; set; } = true;
}
