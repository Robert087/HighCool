namespace ERP.Application.MasterData.ItemComponents;

public sealed record ItemComponentListQuery(Guid? ParentItemId, Guid? ComponentItemId, string? Search);
