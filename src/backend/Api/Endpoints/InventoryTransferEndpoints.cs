using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Transfers;
using ERP.Application.Security;
using ERP.Domain.Common;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class InventoryTransferEndpoints
{
    public static IEndpointRouteBuilder MapInventoryTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var transfers = app.MapGroup("/api/inventory-transfers").RequireAuthorization();
        transfers.MapGet("/", ListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryTransfers))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryStockLedgerView));
        transfers.MapGet("/{id:guid}", GetAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryTransfers))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryStockLedgerView));
        transfers.MapPost("/", CreateDraftAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryTransfers))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryTransferCreate));
        transfers.MapPut("/{id:guid}", UpdateDraftAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryTransfers))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryTransferCreate));
        transfers.MapDelete("/{id:guid}", DeleteDraftAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryTransfers))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryTransferCreate));
        transfers.MapPost("/{id:guid}/post", PostAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryTransfers))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryTransferPost));
        transfers.MapPost("/{id:guid}/cancel", CancelAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryTransfers))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryTransferPost));

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? search,
        string? transferNo,
        Guid? sourceWarehouseId,
        Guid? destinationWarehouseId,
        DocumentStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IInventoryTransferService service,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return Results.BadRequest(new { message = "From date cannot be later than to date." });
        }

        var result = await service.ListAsync(
            new InventoryTransferListQuery(
                search,
                transferNo,
                sourceWarehouseId,
                destinationWarehouseId,
                status,
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
        IInventoryTransferService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateDraftAsync(
        UpsertInventoryTransferRequest request,
        IValidator<UpsertInventoryTransferRequest> validator,
        IInventoryTransferService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedCreateAsync(
            request,
            validator,
            cancellationToken,
            () => service.CreateDraftAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateDraftAsync(
        Guid id,
        UpsertInventoryTransferRequest request,
        IValidator<UpsertInventoryTransferRequest> validator,
        IInventoryTransferService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedUpdateAsync(
            request,
            validator,
            cancellationToken,
            () => service.UpdateDraftAsync(id, request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> DeleteDraftAsync(
        Guid id,
        IInventoryTransferService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await service.DeleteDraftAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (ConcurrencyConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> PostAsync(
        Guid id,
        IInventoryTransferPostingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleDocumentActionAsync(() => service.PostAsync(id, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        IInventoryTransferPostingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleDocumentActionAsync(() => service.CancelAsync(id, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> HandleValidatedCreateAsync<TRequest, TResult>(
        TRequest request,
        IValidator<TRequest> validator,
        CancellationToken cancellationToken,
        Func<Task<TResult>> handler)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
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
        catch (ConcurrencyConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> HandleValidatedUpdateAsync<TRequest, TResult>(
        TRequest request,
        IValidator<TRequest> validator,
        CancellationToken cancellationToken,
        Func<Task<TResult?>> handler)
        where TResult : class
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
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
        catch (ConcurrencyConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> HandleDocumentActionAsync(Func<Task<InventoryTransferDto?>> handler)
    {
        try
        {
            var result = await handler();
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (ConcurrencyConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
    }

    private static string GetActor(HttpContext context)
    {
        return context.User.Identity?.Name ?? "system";
    }
}
