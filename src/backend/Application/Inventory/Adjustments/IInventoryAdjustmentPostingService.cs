namespace ERP.Application.Inventory.Adjustments;

public interface IInventoryAdjustmentPostingService
{
    Task<InventoryAdjustmentDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken);

    Task<InventoryAdjustmentDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken);
}
