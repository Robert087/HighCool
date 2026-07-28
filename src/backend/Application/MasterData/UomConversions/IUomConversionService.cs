using ERP.Application.Common.Pagination;

namespace ERP.Application.MasterData.UomConversions;

public interface IUomConversionService
{
    Task<PagedResult<UomConversionDto>> ListAsync(UomConversionListQuery query, CancellationToken cancellationToken);

    Task<UomConversionDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<UomConversionDto> CreateAsync(UpsertUomConversionRequest request, string actor, CancellationToken cancellationToken);

    Task<UomConversionDto?> UpdateAsync(Guid id, UpsertUomConversionRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
