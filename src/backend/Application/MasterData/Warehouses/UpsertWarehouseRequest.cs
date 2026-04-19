namespace ERP.Application.MasterData.Warehouses;

public sealed record UpsertWarehouseRequest(
    string Code,
    string Name,
    string? Location,
    bool IsActive);
