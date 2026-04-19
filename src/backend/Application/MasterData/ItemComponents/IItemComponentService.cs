namespace ERP.Application.MasterData.ItemComponents;

public interface IItemComponentService
{
    Task<IReadOnlyList<ItemComponentDto>> ListAsync(ItemComponentListQuery query, CancellationToken cancellationToken);

    Task<ItemComponentDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ItemComponentDto> CreateAsync(UpsertItemComponentRequest request, string actor, CancellationToken cancellationToken);

    Task<ItemComponentDto?> UpdateAsync(Guid id, UpsertItemComponentRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
