using ERP.Application.Reversals;
using ERP.Application.Security;
using ERP.Domain.Common;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class ReversalEndpoints
{
    public static IEndpointRouteBuilder MapReversalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/purchase-receipts/{id:guid}/reverse", (Guid id, ReverseDocumentRequest request, IValidator<ReverseDocumentRequest> validator, IReversalService service, HttpContext context, CancellationToken cancellationToken) =>
            ReverseAsync(BusinessDocumentType.PurchaseReceipt, id, request, validator, service, context, cancellationToken))
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Reversals))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.ProcurementPurchaseReceiptCancel));

        app.MapPost("/api/payments/{id:guid}/reverse", (Guid id, ReverseDocumentRequest request, IValidator<ReverseDocumentRequest> validator, IReversalService service, HttpContext context, CancellationToken cancellationToken) =>
            ReverseAsync(BusinessDocumentType.Payment, id, request, validator, service, context, cancellationToken))
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Reversals, OrganizationFeatureKeys.SupplierFinancials))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsReversalsCreate));

        app.MapPost("/api/shortage-resolutions/{id:guid}/reverse", (Guid id, ReverseDocumentRequest request, IValidator<ReverseDocumentRequest> validator, IReversalService service, HttpContext context, CancellationToken cancellationToken) =>
            ReverseAsync(BusinessDocumentType.ShortageResolution, id, request, validator, service, context, cancellationToken))
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Reversals, OrganizationFeatureKeys.ShortageManagement))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.SupplierFinancialsReversalsCreate));

        return app;
    }

    private static async Task<IResult> ReverseAsync(
        BusinessDocumentType documentType,
        Guid id,
        ReverseDocumentRequest request,
        IValidator<ReverseDocumentRequest> validator,
        IReversalService service,
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
            var result = await service.ReverseAsync(documentType, id, request, GetActor(context), cancellationToken);
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
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
    }

    private static string GetActor(HttpContext context)
    {
        return context.User.Identity?.Name ?? "system";
    }
}
