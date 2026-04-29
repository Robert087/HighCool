using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Payments;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Domain.Payments;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var payments = app.MapGroup("/api/payments").RequireAuthorization();
        payments.MapGet("/", ListAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPayablesView));
        payments.MapGet("/{id:guid}", GetAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPayablesView));
        payments.MapGet("/{id:guid}/allocations", GetAllocationsAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPayablesView));
        payments.MapPost("/", CreateDraftAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPaymentsCreate));
        payments.MapPut("/{id:guid}", UpdateDraftAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPaymentsCreate));
        payments.MapPost("/{id:guid}/post", PostAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPaymentsPost));

        app.MapGet("/api/suppliers/{supplierId:guid}/open-balances", ListSupplierOpenBalancesAsync)
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPayablesView));

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? search,
        Guid? supplierId,
        PaymentDirection? direction,
        DocumentStatus? status,
        PaymentMethod? paymentMethod,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IPaymentQueryService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var result = await service.ListAsync(
            new PaymentListQuery(search, supplierId, direction, status, paymentMethod, fromDate, toDate, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Desc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IPaymentQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetAllocationsAsync(
        Guid id,
        IPaymentQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAllocationsAsync(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateDraftAsync(
        UpsertPaymentRequest request,
        IValidator<UpsertPaymentRequest> validator,
        IPaymentService service,
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
        UpsertPaymentRequest request,
        IValidator<UpsertPaymentRequest> validator,
        IPaymentService service,
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
        ISupplierPaymentPostingService service,
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

    private static async Task<IResult> ListSupplierOpenBalancesAsync(
        Guid supplierId,
        PaymentDirection direction,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        ISupplierOpenBalanceService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var result = await service.ListAsync(
            new SupplierOpenBalanceQuery(supplierId, direction, search, fromDate, toDate, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Asc),
            cancellationToken);

        return Results.Ok(result);
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
