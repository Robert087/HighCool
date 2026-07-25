using ERP.Infrastructure.LocalData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ERP.Infrastructure.Security;

public sealed record JwtSigningOptions(string Secret, string Issuer, string Audience);

public static class JwtSigningConfiguration
{
    public const int MinimumSecretLength = 32;

    private static readonly string[] PlaceholderFragments =
    [
        "change-me",
        "changeme",
        "your-secret",
        "example-secret",
        "dev-secret",
        "password",
        "placeholder"
    ];

    public static JwtSigningOptions Resolve(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var issuer = configuration["Authentication:Issuer"] ?? "HighCool";
        var audience = configuration["Authentication:Audience"] ?? "HighCool.Client";
        var configuredSecret = configuration["Authentication:JwtSecret"];
        var secret = string.IsNullOrWhiteSpace(configuredSecret)
            ? ResolveMissingSecret(hostEnvironment)
            : configuredSecret.Trim();

        ValidateSecret(secret);
        return new JwtSigningOptions(secret, issuer, audience);
    }

    public static void ValidateSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Authentication:JwtSecret must be configured.");
        }

        if (secret.Trim().Length < MinimumSecretLength)
        {
            throw new InvalidOperationException($"Authentication:JwtSecret must be at least {MinimumSecretLength} characters.");
        }

        var normalized = secret.Trim().ToLowerInvariant();
        if (PlaceholderFragments.Any(normalized.Contains))
        {
            throw new InvalidOperationException("Authentication:JwtSecret must not contain placeholder text.");
        }
    }

    private static string ResolveMissingSecret(IHostEnvironment hostEnvironment)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
        {
            return GetOrCreateDevelopmentSecret();
        }

        throw new InvalidOperationException("Authentication:JwtSecret must be configured outside Development.");
    }

    private static string GetOrCreateDevelopmentSecret()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(Path.GetTempPath(), "HighCool");
        }

        var keyDirectory = Path.Combine(baseDirectory, "HighCool", "Development", "Keys");
        Directory.CreateDirectory(keyDirectory);
        FilePermissionTools.RestrictToCurrentUser(keyDirectory);

        var keyPath = Path.Combine(keyDirectory, "jwt.key");
        if (File.Exists(keyPath))
        {
            return File.ReadAllText(keyPath).Trim();
        }

        var secret = SecurityTokenTools.CreateToken();
        File.WriteAllText(keyPath, secret);
        FilePermissionTools.RestrictToCurrentUser(keyPath);
        return secret;
    }
}
