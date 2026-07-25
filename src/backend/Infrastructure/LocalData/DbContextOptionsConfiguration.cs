using ERP.Application.LocalData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.LocalData;

public static class DbContextOptionsConfiguration
{
    public static void Configure(
        DbContextOptionsBuilder options,
        ResolvedDatabaseConfiguration databaseConfiguration)
    {
        if (string.Equals(databaseConfiguration.Provider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(databaseConfiguration.ConnectionString);
            return;
        }

        if (string.Equals(databaseConfiguration.Provider, DatabaseProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(databaseConfiguration.ConnectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            return;
        }

        throw new InvalidOperationException($"Unsupported database provider '{databaseConfiguration.Provider}'.");
    }
}
