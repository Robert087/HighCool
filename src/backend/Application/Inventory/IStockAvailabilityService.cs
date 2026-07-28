namespace ERP.Application.Inventory;

public interface IStockAvailabilityService
{
    Task EnsureStockOutAllowedAsync(
        IReadOnlyCollection<StockOutRequirement> requirements,
        CancellationToken cancellationToken);
}

public sealed record StockOutRequirement(
    Guid ItemId,
    Guid WarehouseId,
    decimal BaseQty,
    string Context);
