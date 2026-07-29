namespace ERP.Application.Inventory.Issues;

public interface IInventoryIssuePostingService
{
    Task<InventoryIssueDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken);

    Task<InventoryIssueDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken);
}
