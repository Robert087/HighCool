using ERP.Application.Security;
namespace ERP.Api.Endpoints;

public sealed class OrganizationSetupEndpointFilter(
    bool requireCompletedSetup = true,
    params string[] requiredFeatures) : IEndpointFilter
{
    private readonly bool _requireCompletedSetup = requireCompletedSetup;
    private readonly string[] _requiredFeatures = requiredFeatures;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var executionContext = context.HttpContext.RequestServices.GetRequiredService<IRequestExecutionContext>();
        var featureService = context.HttpContext.RequestServices.GetRequiredService<IOrganizationFeatureService>();

        if (!executionContext.OrganizationId.HasValue)
        {
            return Results.Json(new { message = "Organization access is required." }, statusCode: StatusCodes.Status403Forbidden);
        }

        // TEMPORARILY_DISABLED: Organization setup wizard bypassed until UX/feature mapping is stabilized.
        _ = _requireCompletedSetup;

        foreach (var requiredFeature in _requiredFeatures)
        {
            var feature = OrganizationFeatureKeys.Parse(requiredFeature);
            try
            {
                await featureService.RequireEnabledAsync(feature, context.HttpContext.RequestAborted);
            }
            catch (FeatureDisabledException exception)
            {
                return Results.Json(
                    new
                    {
                        code = FeatureDisabledException.ErrorCode,
                        feature = exception.Feature.ToKey(),
                        message = "This feature is disabled for the active organization."
                    },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        return await next(context);
    }
}
