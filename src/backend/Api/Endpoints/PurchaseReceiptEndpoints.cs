using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Security;
using ERP.Domain.Common;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class PurchaseReceiptEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var receipts = app.MapGroup("/api/purchase-receipts").RequireAuthorization();
        receipts.MapGet("/", ListDraftsAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseReceipts)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseReceiptView));
        receipts.MapGet("/{id:guid}", GetAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseReceipts)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseReceiptView));
        receipts.MapPost("/", CreateDraftAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseReceipts)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseReceiptCreate));
        receipts.MapPut("/{id:guid}", UpdateDraftAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseReceipts)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseReceiptEdit));
        receipts.MapPost("/{id:guid}/post", PostAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Procurement, OrganizationFeatureKeys.PurchaseReceipts)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseReceiptPost));

        return app;
    }

    private static async Task<IResult> ListDraftsAsync(
        string? search,
        DocumentStatus? status,
        bool? linkedToPurchaseOrder,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IPurchaseReceiptService service,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return Results.BadRequest(new { message = "From date cannot be later than to date." });
        }

        var result = await service.ListAsync(
            new PurchaseReceiptListQuery(
                search,
                status,
                linkedToPurchaseOrder,
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
        IPurchaseReceiptService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateDraftAsync(
        UpsertPurchaseReceiptDraftRequest request,
        IValidator<UpsertPurchaseReceiptDraftRequest> validator,
        IPurchaseReceiptService service,
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
        UpsertPurchaseReceiptDraftRequest request,
        IValidator<UpsertPurchaseReceiptDraftRequest> validator,
        IPurchaseReceiptService service,
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
        IPurchaseReceiptPostingService service,
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
