using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class Warehouse : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;
}
