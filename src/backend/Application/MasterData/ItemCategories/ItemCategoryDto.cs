namespace ERP.Application.MasterData.ItemCategories;

public sealed record ItemCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
