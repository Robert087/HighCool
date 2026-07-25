namespace ERP.Application.LocalData;

public sealed record ResolvedDatabaseConfiguration(
    string Provider,
    string ConnectionString,
    string? SqliteDatabasePath);

public interface IDatabaseConfigurationService
{
    ResolvedDatabaseConfiguration GetConfiguration();
}
