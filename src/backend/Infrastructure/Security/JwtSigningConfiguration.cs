using ERP.Infrastructure.LocalData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace ERP.Infrastructure.Security;

public sealed record JwtSigningOptions(string Secret, string Issuer, string Audience);

public static class JwtSigningConfiguration
{
    public const int MinimumSecretLength = 32;
    private const int DevelopmentSecretReadRetryAttempts = 20;

    private static readonly object DevelopmentSecretGate = new();

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
        => GetOrCreateDevelopmentSecret(ResolveDevelopmentKeyPath());

    internal static string ResolveDevelopmentKeyPath(string? baseDirectory = null)
    {
        baseDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(Path.GetTempPath(), "HighCool");
        }

        var keyDirectory = Path.Combine(baseDirectory, "HighCool", "Development", "Keys");
        return Path.Combine(keyDirectory, "jwt.key");
    }

    internal static string GetOrCreateDevelopmentSecret(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            throw new InvalidOperationException("Development JWT signing key path must be configured.");
        }

        lock (DevelopmentSecretGate)
        {
            var existing = TryReadValidDevelopmentSecret(keyPath);
            if (existing is not null)
            {
                return existing;
            }

            var keyDirectory = Path.GetDirectoryName(keyPath);
            if (string.IsNullOrWhiteSpace(keyDirectory))
            {
                throw new InvalidOperationException($"Development JWT signing key path '{keyPath}' must include a directory.");
            }

            Directory.CreateDirectory(keyDirectory);
            FilePermissionTools.RestrictToCurrentUser(keyDirectory);

            var generated = SecurityTokenTools.CreateToken();

            try
            {
                using var stream = new FileStream(
                    keyPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                writer.Write(generated);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            catch (IOException exception) when (File.Exists(keyPath))
            {
                return ReadDevelopmentSecretWithRetry(keyPath, exception);
            }

            FilePermissionTools.RestrictToCurrentUser(keyPath);
            return generated;
        }
    }

    private static string? TryReadValidDevelopmentSecret(string keyPath)
    {
        if (!File.Exists(keyPath))
        {
            return null;
        }

        try
        {
            return ReadValidDevelopmentSecret(keyPath);
        }
        catch (IOException exception) when (File.Exists(keyPath))
        {
            return ReadDevelopmentSecretWithRetry(keyPath, exception);
        }
        catch (UnauthorizedAccessException exception) when (File.Exists(keyPath))
        {
            return ReadDevelopmentSecretWithRetry(keyPath, exception);
        }
    }

    private static string ReadDevelopmentSecretWithRetry(string keyPath, Exception initialException)
    {
        Exception lastException = initialException;

        for (var attempt = 1; attempt <= DevelopmentSecretReadRetryAttempts; attempt++)
        {
            try
            {
                return ReadValidDevelopmentSecret(keyPath);
            }
            catch (IOException exception)
            {
                lastException = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastException = exception;
            }

            if (attempt < DevelopmentSecretReadRetryAttempts)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(attempt * 10));
            }
        }

        throw new InvalidOperationException(
            $"Development JWT signing key at '{keyPath}' could not be read after {DevelopmentSecretReadRetryAttempts} attempts while another process may have been creating it.",
            lastException);
    }

    private static string ReadValidDevelopmentSecret(string keyPath)
    {
        string secret;
        using (var stream = new FileStream(keyPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            secret = reader.ReadToEnd().Trim();
        }

        try
        {
            ValidateSecret(secret);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Development JWT signing key at '{keyPath}' is empty or invalid. Delete the file to let HighCool recreate a development key, or configure Authentication:JwtSecret explicitly.",
                exception);
        }

        return secret;
    }
}
