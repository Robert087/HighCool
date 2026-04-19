namespace ERP.Application.MasterData.ItemComponents;

public sealed record UpsertItemComponentRequest(
    Guid ParentItemId,
    Guid ComponentItemId,
    decimal Quantity);
