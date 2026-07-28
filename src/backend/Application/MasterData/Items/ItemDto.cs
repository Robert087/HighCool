namespace ERP.Application.MasterData.Items;

public sealed record ItemDto(
    Guid Id,
    string Code,
    string Name,
    Guid? CategoryId,
    string? CategoryCode,
    string? CategoryName,
    Guid BaseUomId,
    string BaseUomCode,
    string BaseUomName,
    Guid? DefaultWarehouseId,
    string? DefaultWarehouseCode,
    string? DefaultWarehouseName,
    decimal MinimumStockQuantity,
    bool IsActive,
    bool IsSellable,
    bool HasComponents,
    IReadOnlyList<ItemComponentDto> Components,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
