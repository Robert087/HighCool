using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ERP.Infrastructure.Security;
using ERP.Application.LocalData;
using ERP.Infrastructure.LocalData;

namespace ERP.Infrastructure.Persistence.Factories;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string SqlServerFallbackConnectionString = "";

    private const string SqliteFallbackConnectionString = "Data Source=highcool-dev.db";

    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider")
            ?? Environment.GetEnvironmentVariable("DatabaseProvider")
            ?? DatabaseProviderNames.SqlServer;
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
                ? SqliteFallbackConnectionString
                : SqlServerFallbackConnectionString);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var databaseConfiguration = new ResolvedDatabaseConfiguration(provider, connectionString, null);
        DbContextOptionsConfiguration.Configure(optionsBuilder, databaseConfiguration);

        return new AppDbContext(optionsBuilder.Options, SystemRequestExecutionContext.Instance);
    }
}
