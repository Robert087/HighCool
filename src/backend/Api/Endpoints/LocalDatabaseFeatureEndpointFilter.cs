namespace ERP.Api.Endpoints;

public sealed class LocalDatabaseFeatureEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var hostEnvironment = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var testingLocalDatabaseCapability = hostEnvironment.IsEnvironment("Testing") &&
            bool.TryParse(configuration["LocalDatabase:EnableEndpointCapability"], out var enabled) &&
            enabled;

        if (!hostEnvironment.IsEnvironment("Desktop") && !testingLocalDatabaseCapability)
        {
            return Results.Json(
                new
                {
                    code = "LocalDatabaseFeatureUnavailable",
                    message = "Local database backup and restore endpoints are available only in HighCool Desktop."
                },
                statusCode: StatusCodes.Status409Conflict);
        }

        return await next(context);
    }
}
