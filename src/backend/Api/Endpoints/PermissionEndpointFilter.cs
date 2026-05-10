using ERP.Application.Security;

namespace ERP.Api.Endpoints;

public sealed class PermissionEndpointFilter(string permission) : IEndpointFilter
{
    private readonly string _permission = permission;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var authorizationService = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();

        try
        {
            await authorizationService.EnsurePermissionAsync(_permission, context.HttpContext.RequestAborted);
            return await next(context);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Json(new { message = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
