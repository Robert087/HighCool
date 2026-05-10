using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Domain.Purchasing;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class PurchaseOrderEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var purchaseOrders = app.MapGroup("/api/purchase-orders").RequireAuthorization();
        purchaseOrders.MapGet("/", ListAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseOrders)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseOrderView));
        purchaseOrders.MapGet("/{id:guid}", GetAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseOrders)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseOrderView));
        purchaseOrders.MapGet("/{id:guid}/available-lines-for-receipt", ListAvailableLinesForReceiptAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseOrders, OrganizationFeatureKeys.PurchaseReceipts)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseOrderView));
        purchaseOrders.MapPost("/", CreateDraftAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseOrders)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseOrderCreate));
        purchaseOrders.MapPut("/{id:guid}", UpdateDraftAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseOrders)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseOrderEdit));
        purchaseOrders.MapPost("/{id:guid}/post", PostAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseOrders)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseOrderPost));
        purchaseOrders.MapPost("/{id:guid}/cancel", CancelAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseOrders)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseOrderCancel));

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? search,
        DocumentStatus? status,
        PurchaseOrderReceiptProgressStatus? receiptProgressStatus,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IPurchaseOrderService service,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return Results.BadRequest(new { message = "From date cannot be later than to date." });
        }

        var result = await service.ListAsync(
            new PurchaseOrderListQuery(
                search,
                status,
                receiptProgressStatus,
                fromDate,
                toDate,
                page ?? 1,
                pageSize ?? 20,
                sortBy,
                sortDirection ?? SortDirection.Desc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IPurchaseOrderService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ListAvailableLinesForReceiptAsync(
        Guid id,
        IPurchaseOrderService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ListAvailableLinesForReceiptAsync(id, cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> CreateDraftAsync(
        UpsertPurchaseOrderRequest request,
        IValidator<UpsertPurchaseOrderRequest> validator,
        IPurchaseOrderService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedCreateAsync(
            request,
            validator,
            () => service.CreateDraftAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateDraftAsync(
        Guid id,
        UpsertPurchaseOrderRequest request,
        IValidator<UpsertPurchaseOrderRequest> validator,
        IPurchaseOrderService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedUpdateAsync(
            request,
            validator,
            () => service.UpdateDraftAsync(id, request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> PostAsync(
        Guid id,
        IPurchaseOrderPostingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.PostAsync(id, GetActor(context), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        IPurchaseOrderCancellationService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CancelAsync(id, GetActor(context), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> HandleValidatedCreateAsync<TRequest, TResult>(
        TRequest request,
        IValidator<TRequest> validator,
        Func<Task<TResult>> handler)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await handler();
            return Results.Created(string.Empty, result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> HandleValidatedUpdateAsync<TRequest, TResult>(
        TRequest request,
        IValidator<TRequest> validator,
        Func<Task<TResult?>> handler)
        where TResult : class
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await handler();
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
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
