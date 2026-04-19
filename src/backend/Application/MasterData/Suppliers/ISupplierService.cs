namespace ERP.Application.MasterData.Suppliers;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> ListAsync(SupplierListQuery query, CancellationToken cancellationToken);

    Task<SupplierDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SupplierDto> CreateAsync(UpsertSupplierRequest request, string actor, CancellationToken cancellationToken);

    Task<SupplierDto?> UpdateAsync(Guid id, UpsertSupplierRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
