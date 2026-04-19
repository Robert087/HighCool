using ERP.Domain.MasterData;

namespace ERP.Application.MasterData.ItemUomConversions;

public sealed record UpsertItemUomConversionRequest(
    Guid ItemId,
    Guid FromUomId,
    Guid ToUomId,
    decimal Factor,
    RoundingMode RoundingMode,
    decimal MinFraction,
    bool IsActive);
