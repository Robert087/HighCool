using ERP.Domain.MasterData;

namespace ERP.Application.MasterData.ItemUomConversions;

public sealed record ItemUomConversionDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid FromUomId,
    string FromUomCode,
    Guid ToUomId,
    string ToUomCode,
    decimal Factor,
    RoundingMode RoundingMode,
    decimal MinFraction,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
