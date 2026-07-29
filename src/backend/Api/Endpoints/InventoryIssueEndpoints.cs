using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Issues;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class InventoryIssueEndpoints
{
    public static IEndpointRouteBuilder MapInventoryIssueEndpoints(this IEndpointRouteBuilder app)
    {
        var issues = app.MapGroup("/api/inventory-issues").RequireAuthorization();
        issues.MapGet("/", ListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryIssues))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryIssueView));
        issues.MapGet("/{id:guid}", GetAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryIssues))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryIssueView));
        issues.MapPost("/", CreateDraftAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryIssues))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryIssueCreate));
        issues.MapPut("/{id:guid}", UpdateDraftAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryIssues))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryIssueCreate));
        issues.MapDelete("/{id:guid}", DeleteDraftAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryIssues))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryIssueCreate));
        issues.MapPost("/{id:guid}/post", PostAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryIssues))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryIssuePost));
        issues.MapPost("/{id:guid}/cancel", CancelAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.InventoryIssues))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryIssuePost));

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? search,
        string? issueNo,
        Guid? warehouseId,
        InventoryIssueReason? reason,
        DocumentStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IInventoryIssueService service,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return Results.BadRequest(new { message = "From date cannot be later than to date." });
        }

        var result = await service.ListAsync(
            new InventoryIssueListQuery(
                search,
                issueNo,
                warehouseId,
                reason,
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
        IInventoryIssueService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateDraftAsync(
        UpsertInventoryIssueRequest request,
        IValidator<UpsertInventoryIssueRequest> validator,
        IInventoryIssueService service,
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
        UpsertInventoryIssueRequest request,
        IValidator<UpsertInventoryIssueRequest> validator,
        IInventoryIssueService service,
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
        IInventoryIssueService service,
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
        IInventoryIssuePostingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleDocumentActionAsync(() => service.PostAsync(id, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        IInventoryIssuePostingService service,
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

    private static async Task<IResult> HandleDocumentActionAsync(Func<Task<InventoryIssueDto?>> handler)
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
