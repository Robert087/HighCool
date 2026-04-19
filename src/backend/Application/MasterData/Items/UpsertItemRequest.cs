namespace ERP.Application.MasterData.Items;

public sealed record UpsertItemRequest(
    string Code,
    string Name,
    Guid BaseUomId,
    bool IsActive,
    bool IsSellable,
    bool IsComponent);
