namespace ERP.Application.MasterData.Uoms;

public sealed record UpsertUomRequest(
    string Code,
    string Name,
    int Precision,
    bool AllowsFraction,
    bool IsActive);
