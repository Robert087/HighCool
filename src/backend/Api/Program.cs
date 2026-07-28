using ERP.Api.Endpoints;
using ERP.Application;
using ERP.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        if (builder.Environment.IsEnvironment("Desktop"))
        {
            policy
                .SetIsOriginAllowed(IsAllowedDesktopCorsOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy
                .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsEnvironment("Desktop"))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors("FrontendDev");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    application = "HighCool ERP API",
    status = "Running"
}));

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("OK");
    }
});

app.MapIdentityEndpoints();
app.MapOrganizationSecurityEndpoints();
app.MapMasterDataEndpoints();
app.MapItemMasterDataEndpoints();
app.MapPurchaseOrderEndpoints();
app.MapPurchaseReceiptEndpoints();
app.MapPurchaseReturnEndpoints();
app.MapShortageReasonCodeEndpoints();
app.MapShortageResolutionEndpoints();
app.MapPaymentEndpoints();
app.MapSupplierStatementEndpoints();
app.MapStockLedgerEndpoints();
app.MapInventoryAdjustmentEndpoints();
app.MapReversalEndpoints();
app.MapLocalDatabaseEndpoints();
app.MapDesktopEndpoints();

if (app.Environment.IsEnvironment("Desktop"))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

static bool IsAllowedDesktopCorsOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (uri.Scheme is "http" or "https" &&
        string.Equals(uri.Host, "tauri.localhost", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return uri.Scheme == "http" &&
        !uri.IsDefaultPort &&
        uri.Port > 0 &&
        (string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase));
}

public partial class Program;
