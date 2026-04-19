namespace ERP.Application.MasterData.ItemUomConversions;

public sealed record ItemUomConversionListQuery(Guid? ItemId, bool? IsActive, string? Search);
