using ERP.Application.Security;

namespace ERP.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/signup", SignupAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/forgot-password", ForgotPasswordAsync);
        auth.MapPost("/reset-password", ResetPasswordAsync);
        auth.MapPost("/request-email-verification", RequestEmailVerificationAsync);
        auth.MapPost("/verify-email", VerifyEmailAsync);
        auth.MapPost("/accept-invitation", AcceptInvitationAsync);

        var protectedAuth = auth.MapGroup(string.Empty).RequireAuthorization();
        protectedAuth.MapPost("/logout", LogoutAsync);
        protectedAuth.MapGet("/me", MeAsync);
        protectedAuth.MapPost("/switch-organization", SwitchOrganizationAsync);

        return app;
    }

    private static Task<IResult> SignupAsync(SignupRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.SignupAsync(request, cancellationToken));

    private static Task<IResult> LoginAsync(LoginRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.LoginAsync(request, cancellationToken));

    private static Task<IResult> LogoutAsync(LogoutRequest? request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            await service.LogoutAsync(request?.AllDevices ?? false, cancellationToken);
            return Results.NoContent();
        });

    private static Task<IResult> MeAsync(IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.GetCurrentWorkspaceAsync(cancellationToken));

    private static Task<IResult> SwitchOrganizationAsync(SwitchOrganizationRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.SwitchOrganizationAsync(request, cancellationToken));

    private static Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () => Results.Ok(new
        {
            token = await service.RequestPasswordResetAsync(request, cancellationToken)
        }));

    private static Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            await service.ResetPasswordAsync(request, cancellationToken);
            return Results.NoContent();
        });

    private static Task<IResult> RequestEmailVerificationAsync(RequestEmailVerificationRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () => Results.Ok(new
        {
            token = await service.RequestEmailVerificationAsync(request, cancellationToken)
        }));

    private static Task<IResult> VerifyEmailAsync(ConfirmEmailVerificationRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            await service.ConfirmEmailVerificationAsync(request, cancellationToken);
            return Results.NoContent();
        });

    private static Task<IResult> AcceptInvitationAsync(AcceptInvitationRequest request, IIdentityService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            await service.AcceptInvitationAsync(request, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Json(new { message = exception.Message }, statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Json(new { message = exception.Message }, statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
