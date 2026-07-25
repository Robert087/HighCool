using ERP.Application.LocalData;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.LocalData;

public sealed class DatabaseConfigurationService(
    IConfiguration configuration,
    ILocalStoragePathService localStoragePathService) : IDatabaseConfigurationService
{
    public ResolvedDatabaseConfiguration GetConfiguration()
    {
        var provider = configuration[$"{DatabaseOptions.SectionName}:Provider"]
            ?? configuration["DatabaseProvider"]
            ?? DatabaseProviderNames.SqlServer;

        if (string.Equals(provider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = configuration[$"{DatabaseOptions.SectionName}:SqliteFileName"] ?? "highcool.db";
            var databasePath = localStoragePathService.GetSqliteDatabasePath(fileName);
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath
            }.ToString();

            return new ResolvedDatabaseConfiguration(DatabaseProviderNames.Sqlite, connectionString, databasePath);
        }

        if (string.Equals(provider, DatabaseProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured when Database:Provider is SqlServer.");
            }

            return new ResolvedDatabaseConfiguration(DatabaseProviderNames.SqlServer, connectionString, null);
        }

        throw new InvalidOperationException($"Unsupported database provider '{provider}'. Supported providers are Sqlite and SqlServer.");
    }
}
