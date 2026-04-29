using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ERP.Infrastructure.Security;

namespace ERP.Infrastructure.Persistence.Factories;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string SqlServerFallbackConnectionString =
        "Server=localhost,1433;Database=ERPDb;User Id=sa;Password=YourStrong@Pass123;TrustServerCertificate=True";

    private const string SqliteFallbackConnectionString = "Data Source=highcool-dev.db";

    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("DatabaseProvider") ?? "SqlServer";
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
                ? SqliteFallbackConnectionString
                : SqlServerFallbackConnectionString);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        }

        return new AppDbContext(optionsBuilder.Options, SystemRequestExecutionContext.Instance);
    }
}
