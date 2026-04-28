using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Shortages;
using ERP.Domain.Common;
using ERP.Domain.Shortages;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class ShortageResolutionEndpoints
{
    public static IEndpointRouteBuilder MapShortageResolutionEndpoints(this IEndpointRouteBuilder app)
    {
        var shortages = app.MapGroup("/api/shortages");
        shortages.MapGet("/open", ListOpenShortagesAsync);
        shortages.MapGet("/{id:guid}", GetShortageAsync);

        var resolutions = app.MapGroup("/api/shortage-resolutions");
        resolutions.MapGet("/", ListResolutionsAsync);
        resolutions.MapGet("/{id:guid}", GetResolutionAsync);
        resolutions.MapGet("/{id:guid}/allocations", GetAllocationsAsync);
        resolutions.MapPost("/", CreateDraftAsync);
        resolutions.MapPut("/{id:guid}", UpdateDraftAsync);
        resolutions.MapPost("/{id:guid}/post", PostAsync);
        resolutions.MapPost("/suggest-allocations", SuggestAllocationsAsync);

        return app;
    }

    private static async Task<IResult> ListOpenShortagesAsync(
        string? search,
        Guid? supplierId,
        Guid? itemId,
        Guid? componentItemId,
        bool? affectsSupplierBalance,
        ShortageEntryStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IShortageResolutionService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var result = await service.ListOpenShortagesAsync(
            new OpenShortageQuery(search, supplierId, itemId, componentItemId, affectsSupplierBalance, status, fromDate, toDate, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Asc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetShortageAsync(
        Guid id,
        IShortageResolutionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetShortageAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ListResolutionsAsync(
        string? search,
        Guid? supplierId,
        ShortageResolutionType? resolutionType,
        DocumentStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IShortageResolutionService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var result = await service.ListAsync(
            new ShortageResolutionListQuery(search, supplierId, resolutionType, status, fromDate, toDate, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Desc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetResolutionAsync(
        Guid id,
        IShortageResolutionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetAllocationsAsync(
        Guid id,
        IShortageResolutionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAllocationsAsync(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateDraftAsync(
        UpsertShortageResolutionRequest request,
        IValidator<UpsertShortageResolutionRequest> validator,
        IShortageResolutionService service,
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
        UpsertShortageResolutionRequest request,
        IValidator<UpsertShortageResolutionRequest> validator,
        IShortageResolutionService service,
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
        IShortageResolutionPostingService service,
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

    private static async Task<IResult> SuggestAllocationsAsync(
        SuggestShortageAllocationsQuery request,
        IShortageResolutionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.SuggestAllocationsAsync(request, cancellationToken);
            return Results.Ok(result);
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

    private static string? ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return "From date cannot be later than to date.";
        }

        return null;
    }

    private static string GetActor(HttpContext context)
    {
        return context.User.Identity?.Name ?? "system";
    }
}
