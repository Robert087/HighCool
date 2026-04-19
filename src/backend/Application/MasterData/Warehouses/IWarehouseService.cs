namespace ERP.Application.MasterData.Warehouses;

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> ListAsync(WarehouseListQuery query, CancellationToken cancellationToken);

    Task<WarehouseDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<WarehouseDto> CreateAsync(UpsertWarehouseRequest request, string actor, CancellationToken cancellationToken);

    Task<WarehouseDto?> UpdateAsync(Guid id, UpsertWarehouseRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
