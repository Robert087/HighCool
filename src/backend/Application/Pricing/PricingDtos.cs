using ERP.Application.Common.Pagination;
using ERP.Domain.Pricing;

namespace ERP.Application.Pricing;

public sealed record PriceListListQuery(
    string? Search,
    string? Code,
    string? Name,
    PriceListType? Type,
    string? Currency,
    bool? IsActive,
    bool? IsDefault,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc);

public sealed record ItemPriceListQuery(
    string? Search,
    Guid? PriceListId,
    PriceListType? PriceListType,
    Guid? ItemId,
    Guid? CategoryId,
    Guid? UomId,
    string? Currency,
    bool? IsActive,
    DateTime? EffectiveOn,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc);

public sealed record UpsertPriceListRequest(
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    bool IsDefault,
    bool IsActive,
    string? Description);

public sealed record UpdatePriceListRequest(
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    bool IsDefault,
    bool IsActive,
    string? Description,
    int Version);

public sealed record UpsertItemPriceRequest(
    Guid PriceListId,
    Guid ItemId,
    Guid UomId,
    string? Currency,
    decimal Rate,
    decimal MinimumQuantity,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool IsActive,
    string? Notes);

public sealed record UpdateItemPriceRequest(
    Guid PriceListId,
    Guid ItemId,
    Guid UomId,
    string? Currency,
    decimal Rate,
    decimal MinimumQuantity,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool IsActive,
    string? Notes,
    int Version);

public sealed record VersionRequest(int Version);

public sealed record PriceListDto(
    Guid Id,
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    bool IsDefault,
    bool IsActive,
    string? Description,
    int ItemPriceCount,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ItemPriceDto(
    Guid Id,
    Guid PriceListId,
    string PriceListCode,
    string PriceListName,
    PriceListType PriceListType,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid? CategoryId,
    string? CategoryCode,
    string? CategoryName,
    Guid UomId,
    string UomCode,
    string UomName,
    string Currency,
    decimal Rate,
    decimal MinimumQuantity,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsActive,
    bool IsCurrentlyEffective,
    string? Notes,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PricingFilterOptionsDto(
    IReadOnlyList<PricingOptionDto> PriceLists,
    IReadOnlyList<PricingOptionDto> Items,
    IReadOnlyList<PricingOptionDto> Uoms,
    IReadOnlyList<PricingOptionDto> Categories,
    IReadOnlyList<string> Currencies);

public sealed record PricingOptionDto(Guid Id, string Code, string Name, string? Currency = null);

public sealed record ItemPricingUomOptionsDto(Guid ItemId, IReadOnlyList<PricingOptionDto> Uoms);

public sealed record PriceResolutionQuery(
    Guid PriceListId,
    Guid ItemId,
    Guid UomId,
    decimal Quantity,
    DateTime? EffectiveDate);

public sealed record PriceResolutionDto(
    Guid ItemPriceId,
    Guid PriceListId,
    Guid ItemId,
    Guid UomId,
    string Currency,
    decimal Rate,
    decimal MinimumQuantity,
    DateTime ValidFrom,
    DateTime? ValidTo);
