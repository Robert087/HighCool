namespace ERP.Application.MasterData.Warehouses;

public sealed record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? Location,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
