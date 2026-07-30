using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory.Monitoring;

public interface IInventoryMonitoringService
{
    Task<InventoryMonitoringDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);

    Task<InventoryMonitoringFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);

    Task<PagedResult<InventoryMonitoringItemDto>> ListItemsAsync(
        InventoryMonitoringListQuery query,
        CancellationToken cancellationToken);

    Task<ReorderSettingsDto?> GetReorderSettingsAsync(Guid itemId, CancellationToken cancellationToken);

    Task<ReorderSettingsDto?> UpdateReorderSettingsAsync(
        Guid itemId,
        UpdateReorderSettingsRequest request,
        string actor,
        CancellationToken cancellationToken);
}
