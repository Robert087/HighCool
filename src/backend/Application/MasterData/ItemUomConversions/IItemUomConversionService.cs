namespace ERP.Application.MasterData.ItemUomConversions;

public interface IItemUomConversionService
{
    Task<IReadOnlyList<ItemUomConversionDto>> ListAsync(ItemUomConversionListQuery query, CancellationToken cancellationToken);

    Task<ItemUomConversionDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ItemUomConversionDto> CreateAsync(UpsertItemUomConversionRequest request, string actor, CancellationToken cancellationToken);

    Task<ItemUomConversionDto?> UpdateAsync(Guid id, UpsertItemUomConversionRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
