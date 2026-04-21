using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Infrastructure.Persistence.Factories;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string SqliteFallbackConnectionString = "Data Source=highcool-dev.db";

    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("DatabaseProvider") ?? "Sqlite";
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? SqliteFallbackConnectionString;

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

        return new AppDbContext(optionsBuilder.Options);
    }
}
