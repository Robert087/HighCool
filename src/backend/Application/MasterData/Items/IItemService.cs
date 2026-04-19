namespace ERP.Application.MasterData.Items;

public interface IItemService
{
    Task<IReadOnlyList<ItemDto>> ListAsync(ItemListQuery query, CancellationToken cancellationToken);

    Task<ItemDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ItemDto> CreateAsync(UpsertItemRequest request, string actor, CancellationToken cancellationToken);

    Task<ItemDto?> UpdateAsync(Guid id, UpsertItemRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
