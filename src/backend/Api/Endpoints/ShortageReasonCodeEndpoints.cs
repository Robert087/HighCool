using ERP.Application.Purchasing.ShortageReasonCodes;
using ERP.Application.Security;

namespace ERP.Api.Endpoints;

public static class ShortageReasonCodeEndpoints
{
    public static IEndpointRouteBuilder MapShortageReasonCodeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shortage-reason-codes", ListAsync)
            .RequireAuthorization()
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.ShortageManagement))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.ShortageView));
        return app;
    }

    private static async Task<IResult> ListAsync(
        IShortageReasonCodeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListActiveAsync(cancellationToken);
        return Results.Ok(result);
    }
}
