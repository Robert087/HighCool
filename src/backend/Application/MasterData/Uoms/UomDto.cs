namespace ERP.Application.MasterData.Uoms;

public sealed record UomDto(
    Guid Id,
    string Code,
    string Name,
    int Precision,
    bool AllowsFraction,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
