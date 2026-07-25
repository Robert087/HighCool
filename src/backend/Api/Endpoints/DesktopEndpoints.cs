using ERP.Application.LocalData;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Api.Endpoints;

public static class DesktopEndpoints
{
    private const string StartupTokenHeader = "X-HighCool-Startup-Token";

    public static IEndpointRouteBuilder MapDesktopEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/desktop");

        group.MapGet("/startup-diagnostics", StartupDiagnosticsAsync);
        group.MapPost("/shutdown", ShutdownAsync);

        return app;
    }

    private static async Task<IResult> StartupDiagnosticsAsync(
        HttpContext context,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        IStartupDiagnosticsService diagnosticsService,
        CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsEnvironment("Desktop"))
        {
            return Results.NotFound();
        }

        if (!IsLoopbackRequest(context))
        {
            return Results.NotFound();
        }

        var expectedToken = configuration["Desktop:StartupToken"];
        if (string.IsNullOrWhiteSpace(expectedToken) ||
            !context.Request.Headers.TryGetValue(StartupTokenHeader, out var providedToken) ||
            !FixedTimeEquals(expectedToken, providedToken.ToString()))
        {
            return Results.Unauthorized();
        }

        var diagnostics = await diagnosticsService.GetAsync(cancellationToken);

        return Results.Ok(new DesktopStartupDiagnosticsResponse(
            "HighCool",
            Environment.ProcessId,
            diagnostics));
    }

    private static IResult ShutdownAsync(
        HttpContext context,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime)
    {
        if (!hostEnvironment.IsEnvironment("Desktop"))
        {
            return Results.NotFound();
        }

        if (!IsLoopbackRequest(context))
        {
            return Results.NotFound();
        }

        var expectedToken = configuration["Desktop:StartupToken"];
        if (string.IsNullOrWhiteSpace(expectedToken) ||
            !context.Request.Headers.TryGetValue(StartupTokenHeader, out var providedToken) ||
            !FixedTimeEquals(expectedToken, providedToken.ToString()))
        {
            return Results.Unauthorized();
        }

        _ = Task.Run(applicationLifetime.StopApplication);

        return Results.Accepted();
    }

    private static bool IsLoopbackRequest(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        return remoteIp is not null && IPAddress.IsLoopback(remoteIp);
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}

public sealed record DesktopStartupDiagnosticsResponse(
    string Application,
    int ProcessId,
    StartupDiagnosticResult Diagnostics);
