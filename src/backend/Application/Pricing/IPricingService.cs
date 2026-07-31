using ERP.Application.Common.Pagination;

namespace ERP.Application.Pricing;

public interface IPricingService
{
    Task<PagedResult<PriceListDto>> ListPriceListsAsync(PriceListListQuery query, CancellationToken cancellationToken);

    Task<PriceListDto?> GetPriceListAsync(Guid id, CancellationToken cancellationToken);

    Task<PriceListDto> CreatePriceListAsync(UpsertPriceListRequest request, string actor, CancellationToken cancellationToken);

    Task<PriceListDto?> UpdatePriceListAsync(Guid id, UpdatePriceListRequest request, string actor, CancellationToken cancellationToken);

    Task<PriceListDto?> ActivatePriceListAsync(Guid id, int version, string actor, CancellationToken cancellationToken);

    Task<PriceListDto?> DeactivatePriceListAsync(Guid id, int version, string actor, CancellationToken cancellationToken);

    Task<bool> DeletePriceListAsync(Guid id, int version, CancellationToken cancellationToken);

    Task<PagedResult<ItemPriceDto>> ListItemPricesAsync(ItemPriceListQuery query, CancellationToken cancellationToken);

    Task<ItemPriceDto?> GetItemPriceAsync(Guid id, CancellationToken cancellationToken);

    Task<ItemPriceDto> CreateItemPriceAsync(UpsertItemPriceRequest request, string actor, CancellationToken cancellationToken);

    Task<ItemPriceDto?> UpdateItemPriceAsync(Guid id, UpdateItemPriceRequest request, string actor, CancellationToken cancellationToken);

    Task<ItemPriceDto?> ActivateItemPriceAsync(Guid id, int version, string actor, CancellationToken cancellationToken);

    Task<ItemPriceDto?> DeactivateItemPriceAsync(Guid id, int version, string actor, CancellationToken cancellationToken);

    Task<bool> DeleteItemPriceAsync(Guid id, int version, CancellationToken cancellationToken);

    Task<PriceResolutionDto?> ResolvePriceAsync(PriceResolutionQuery query, CancellationToken cancellationToken);

    Task<PricingFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);

    Task<ItemPricingUomOptionsDto?> GetItemUomOptionsAsync(Guid itemId, CancellationToken cancellationToken);
}
