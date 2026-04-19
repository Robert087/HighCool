namespace ERP.Application.MasterData.ItemComponents;

public sealed record ItemComponentDto(
    Guid Id,
    Guid ParentItemId,
    string ParentItemCode,
    string ParentItemName,
    Guid ComponentItemId,
    string ComponentItemCode,
    string ComponentItemName,
    decimal Quantity,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
