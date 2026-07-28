namespace ERP.Application.MasterData.ItemCategories;

public sealed record UpsertItemCategoryRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);
