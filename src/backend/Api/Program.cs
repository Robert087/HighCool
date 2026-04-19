using ERP.Api.Endpoints;
using ERP.Application;
using ERP.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

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

app.MapMasterDataEndpoints();
app.MapItemMasterDataEndpoints();

app.Run();
