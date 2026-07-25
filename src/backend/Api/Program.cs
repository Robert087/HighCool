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
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
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
app.MapReversalEndpoints();
app.MapLocalDatabaseEndpoints();
app.MapDesktopEndpoints();

if (app.Environment.IsEnvironment("Desktop"))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
