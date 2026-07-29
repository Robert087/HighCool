using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory.Issues;

public interface IInventoryIssueService
{
    Task<PagedResult<InventoryIssueListItemDto>> ListAsync(
        InventoryIssueListQuery query,
        CancellationToken cancellationToken);

    Task<InventoryIssueDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<InventoryIssueDto> CreateDraftAsync(
        UpsertInventoryIssueRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<InventoryIssueDto?> UpdateDraftAsync(
        Guid id,
        UpsertInventoryIssueRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken);
}
