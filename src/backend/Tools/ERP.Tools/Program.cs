using ERP.Application;
using ERP.Application.Security;
using ERP.Application.TestData;
using ERP.Infrastructure;
using ERP.Infrastructure.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

var parsed = CommandLine.Parse(args);
if (parsed is null)
{
    PrintUsage();
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddOrganizationTestDataTools();
builder.Services.RemoveAll<IRequestExecutionContext>();
builder.Services.RemoveAll<IOrganizationScopedToolExecutionContext>();
builder.Services.AddSingleton<OrganizationToolExecutionContext>();
builder.Services.AddSingleton<IRequestExecutionContext>(provider => provider.GetRequiredService<OrganizationToolExecutionContext>());
builder.Services.AddSingleton<IOrganizationScopedToolExecutionContext>(provider => provider.GetRequiredService<OrganizationToolExecutionContext>());

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<IOrganizationTestDataService>();

OrganizationTestDataCommandResult result;
try
{
    result = parsed.Command switch
    {
        "seed-org-test-data" => await service.SeedAsync(
            new SeedOrganizationTestDataRequest(
                parsed.RequireGuid("organization-id"),
                parsed.Get("profile") ?? "restore-smoke",
                parsed.Get("scale") ?? "small",
                parsed.GetInt("seed") ?? 1,
                parsed.Has("dry-run"),
                parsed.Has("force")),
            CancellationToken.None),
        "reset-org-data" => await service.ResetAsync(
            new ResetOrganizationDataRequest(
                parsed.RequireGuid("organization-id"),
                parsed.Has("dry-run") || !parsed.Has("execute"),
                parsed.Has("execute"),
                parsed.Get("confirmation"),
                parsed.Has("preserve-users") || !parsed.Has("delete-users"),
                parsed.Has("preserve-organization") || !parsed.Has("delete-organization"),
                parsed.Has("preserve-settings") || !parsed.Has("delete-settings"),
                parsed.Has("test-data-only"),
                parsed.Get("seed-run-id"),
                parsed.Has("skip-safety-backup")),
            CancellationToken.None),
        "verify-org-restore" => await service.VerifyAsync(
            new VerifyOrganizationRestoreRequest(
                parsed.RequireGuid("organization-id"),
                parsed.Require("snapshot")),
            CancellationToken.None),
        _ => throw new InvalidOperationException("Unsupported command.")
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
}));

return result.Status is OrganizationTestDataCommandStatus.Completed or OrganizationTestDataCommandStatus.Planned ? 0 : 1;

static void PrintUsage()
{
    Console.WriteLine("HighCool organization test data tools");
    Console.WriteLine("Commands:");
    Console.WriteLine("  seed-org-test-data --organization-id <guid> [--profile restore-smoke] [--scale small|medium|large] [--seed 1] [--dry-run] [--force]");
    Console.WriteLine("  reset-org-data --organization-id <guid> [--test-data-only --seed-run-id <id>] [--dry-run] [--execute --confirmation RESET-ORG-<guid>] [--skip-safety-backup]");
    Console.WriteLine("  verify-org-restore --organization-id <guid> --snapshot <path>");
}

internal sealed class CommandLine
{
    private readonly Dictionary<string, string?> _options;

    private CommandLine(string command, Dictionary<string, string?> options)
    {
        Command = command;
        _options = options;
    }

    public string Command { get; }

    public static CommandLine? Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            return null;
        }

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            var key = arg[2..];
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = args[++index];
            }
            else
            {
                options[key] = null;
            }
        }

        return new CommandLine(args[0], options);
    }

    public bool Has(string key) => _options.ContainsKey(key);

    public string? Get(string key) => _options.TryGetValue(key, out var value) ? value : null;

    public string Require(string key)
    {
        var value = Get(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required --{key} option.");
        }

        return value;
    }

    public Guid RequireGuid(string key)
    {
        var value = Require(key);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"--{key} must be a valid GUID.");
    }

    public int? GetInt(string key)
    {
        var value = Get(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"--{key} must be an integer.");
    }
}
