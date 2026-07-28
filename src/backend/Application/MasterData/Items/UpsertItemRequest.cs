namespace ERP.Application.MasterData.Items;

public sealed record UpsertItemRequest(
    string Code,
    string Name,
    Guid? CategoryId,
    Guid BaseUomId,
    Guid? DefaultWarehouseId,
    decimal MinimumStockQuantity,
    bool IsActive,
    bool IsSellable,
    bool HasComponents,
    IReadOnlyList<UpsertItemComponentRequest> Components);
