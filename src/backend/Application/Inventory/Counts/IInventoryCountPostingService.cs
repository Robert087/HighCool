namespace ERP.Application.Inventory.Counts;

public interface IInventoryCountPostingService
{
    Task<InventoryCountDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken);

    Task<InventoryCountDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken);
}
