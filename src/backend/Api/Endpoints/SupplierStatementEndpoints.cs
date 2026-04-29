using ERP.Application.Statements;
using ERP.Application.Common.Pagination;
using ERP.Application.Security;
using ERP.Domain.Statements;

namespace ERP.Api.Endpoints;

public static class SupplierStatementEndpoints
{
    public static IEndpointRouteBuilder MapSupplierStatementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/supplier-statements", ListAsync)
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPayablesView));
        app.MapGet("/api/suppliers/{supplierId:guid}/statement", ListForSupplierAsync)
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPayablesView));
        app.MapGet("/api/suppliers/{supplierId:guid}/statement/summary", GetSummaryAsync)
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierFinancials))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsPayablesView));

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? search,
        Guid? supplierId,
        SupplierStatementEffectType? effectType,
        SupplierStatementSourceDocumentType? sourceDocType,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        ISupplierStatementQueryService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var result = await service.ListAsync(
            new SupplierStatementQuery(search, supplierId, effectType, sourceDocType, fromDate, toDate, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Desc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static Task<IResult> ListForSupplierAsync(
        Guid supplierId,
        string? search,
        SupplierStatementEffectType? effectType,
        SupplierStatementSourceDocumentType? sourceDocType,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        ISupplierStatementQueryService service,
        CancellationToken cancellationToken)
    {
        return ListAsync(
            search,
            supplierId,
            effectType,
            sourceDocType,
            fromDate,
            toDate,
            page,
            pageSize,
            sortBy,
            sortDirection,
            service,
            cancellationToken);
    }

    private static async Task<IResult> GetSummaryAsync(
        Guid supplierId,
        SupplierStatementEffectType? effectType,
        SupplierStatementSourceDocumentType? sourceDocType,
        DateTime? fromDate,
        DateTime? toDate,
        ISupplierBalanceService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var result = await service.GetSummaryAsync(
            new SupplierStatementSummaryQuery(supplierId, effectType, sourceDocType, fromDate, toDate),
            cancellationToken);

        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static string? ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return "From date cannot be later than to date.";
        }

        return null;
    }
}
