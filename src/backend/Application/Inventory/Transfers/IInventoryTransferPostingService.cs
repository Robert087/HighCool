namespace ERP.Application.Inventory.Transfers;

public interface IInventoryTransferPostingService
{
    Task<InventoryTransferDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken);

    Task<InventoryTransferDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken);
}
