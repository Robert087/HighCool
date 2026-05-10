using ERP.Api.Endpoints;
using ERP.Application;
using ERP.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);
var allowedFrontendOrigins = GetAllowedFrontendOrigins(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedFrontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);
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
app.MapReversalEndpoints();

app.Run();

static string[] GetAllowedFrontendOrigins(IConfiguration configuration)
{
    var configuredOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    var commaSeparatedOrigins = configuration["Cors:AllowedOrigins"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    var origins = configuredOrigins
        .Concat(commaSeparatedOrigins)
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return origins.Length > 0
        ? origins
        :
        [
            "http://localhost:5173",
            "http://localhost:4173",
            "https://high-cool-production.vercel.app"
        ];
}

public partial class Program;
