using ERP.Application.Security;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        if (!executionContext.OrganizationId.HasValue)
        {
            return Results.Json(new { message = "Organization access is required." }, statusCode: StatusCodes.Status403Forbidden);
        }

        var organization = await dbContext.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == executionContext.OrganizationId.Value, context.HttpContext.RequestAborted);

        // TEMPORARILY_DISABLED: Organization setup wizard bypassed until UX/feature mapping is stabilized.
        // TEMPORARILY_DISABLED: Feature gating bypassed until all module keys/routes are aligned.
        _ = organization;
        _ = _requireCompletedSetup;
        _ = _requiredFeatures;

        return await next(context);
    }
}
