using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Monitoring;
using ERP.Application.Security;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class InventoryMonitoringEndpoints
{
    public static IEndpointRouteBuilder MapInventoryMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        var monitor = app.MapGroup("/api/inventory/monitor").RequireAuthorization();
        monitor.MapGet("/dashboard", GetDashboardAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.LowStockAlerts))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryMonitorView));
        monitor.MapGet("/filter-options", GetFilterOptionsAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.LowStockAlerts))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryMonitorView));
        monitor.MapGet("/items", ListItemsAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.LowStockAlerts))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryMonitorView));

        var itemSettings = app.MapGroup("/api/inventory/items").RequireAuthorization();
        itemSettings.MapGet("/{id:guid}/reorder-settings", GetReorderSettingsAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.LowStockAlerts))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryMonitorView));
        itemSettings.MapPut("/{id:guid}/reorder-settings", UpdateReorderSettingsAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.LowStockAlerts))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryMonitorManage));

        return app;
    }

    private static async Task<IResult> GetDashboardAsync(
        IInventoryMonitoringService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetDashboardAsync(cancellationToken));
    }

    private static async Task<IResult> GetFilterOptionsAsync(
        IInventoryMonitoringService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetFilterOptionsAsync(cancellationToken));
    }

    private static async Task<IResult> ListItemsAsync(
        string? search,
        Guid? warehouseId,
        Guid? categoryId,
        InventoryStockStatus? status,
        bool? onlyMonitored,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IInventoryMonitoringService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListItemsAsync(
            new InventoryMonitoringListQuery(
                search,
                warehouseId,
                categoryId,
                status,
                onlyMonitored ?? true,
                page ?? 1,
                pageSize ?? 20,
                sortBy,
                sortDirection ?? SortDirection.Asc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetReorderSettingsAsync(
        Guid id,
        IInventoryMonitoringService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetReorderSettingsAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UpdateReorderSettingsAsync(
        Guid id,
        UpdateReorderSettingsRequest request,
        IValidator<UpdateReorderSettingsRequest> validator,
        IInventoryMonitoringService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await service.UpdateReorderSettingsAsync(id, request, GetActor(context), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
    }

    private static string GetActor(HttpContext context)
    {
        return context.User.Identity?.Name ?? "system";
    }
}
