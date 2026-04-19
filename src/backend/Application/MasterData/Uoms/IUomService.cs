namespace ERP.Application.MasterData.Uoms;

public interface IUomService
{
    Task<IReadOnlyList<UomDto>> ListAsync(UomListQuery query, CancellationToken cancellationToken);

    Task<UomDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<UomDto> CreateAsync(UpsertUomRequest request, string actor, CancellationToken cancellationToken);

    Task<UomDto?> UpdateAsync(Guid id, UpsertUomRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
