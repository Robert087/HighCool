using ERP.Application.LocalData;
using ERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.Infrastructure.LocalData;

public sealed class DatabaseHealthService(
    AppDbContext dbContext,
    IDatabaseConfigurationService databaseConfigurationService,
    IApplicationDatabaseMetadataService metadataService) : IDatabaseHealthService
{
    public async Task<DatabaseHealthResult> CheckAsync(bool requireWritable, CancellationToken cancellationToken)
    {
        var databaseConfiguration = databaseConfigurationService.GetConfiguration();

        if (string.Equals(databaseConfiguration.Provider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(databaseConfiguration.SqliteDatabasePath) &&
            !File.Exists(databaseConfiguration.SqliteDatabasePath))
        {
            return new DatabaseHealthResult(DatabaseHealthStatus.Missing, "The configured local database does not exist.");
        }

        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return new DatabaseHealthResult(DatabaseHealthStatus.Unavailable, "The configured database could not be opened.");
            }

            if (dbContext.Database.IsSqlite())
            {
                var integrityCheck = await RunSqliteIntegrityCheckAsync(cancellationToken);
                if (!string.Equals(integrityCheck, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return new DatabaseHealthResult(DatabaseHealthStatus.Corrupt, "SQLite integrity check failed.");
                }

                if (!await SqliteTableExistsAsync("application_database_metadata", cancellationToken))
                {
                    return new DatabaseHealthResult(DatabaseHealthStatus.UnsupportedSchema, "Database metadata is missing.");
                }
            }

            var metadata = await metadataService.GetCurrentAsync(cancellationToken);
            if (metadata is null)
            {
                return new DatabaseHealthResult(DatabaseHealthStatus.UnsupportedSchema, "Database metadata is missing.");
            }

            if (metadata.DatabaseSchemaVersion > DatabaseSchemaInfo.CurrentSchemaVersion)
            {
                return new DatabaseHealthResult(
                    DatabaseHealthStatus.UnsupportedSchema,
                    "The database schema is newer than this HighCool build supports.",
                    metadata.DatabaseSchemaVersion,
                    metadata.ApplicationVersion);
            }

            if (requireWritable && !await IsWritableAsync(cancellationToken))
            {
                return new DatabaseHealthResult(
                    DatabaseHealthStatus.ReadOnly,
                    "The database can be opened but is not writable.",
                    metadata.DatabaseSchemaVersion,
                    metadata.ApplicationVersion);
            }

            return new DatabaseHealthResult(
                DatabaseHealthStatus.Healthy,
                "Database is healthy.",
                metadata.DatabaseSchemaVersion,
                metadata.ApplicationVersion);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 11)
        {
            return new DatabaseHealthResult(DatabaseHealthStatus.Corrupt, "SQLite reported database corruption.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("newer than this HighCool build", StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseHealthResult(DatabaseHealthStatus.UnsupportedSchema, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            return new DatabaseHealthResult(DatabaseHealthStatus.Unavailable, "The configured database is unavailable.");
        }
    }

    private async Task<string> RunSqliteIntegrityCheckAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> IsWritableAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                CREATE TABLE "__highcool_write_probe" ("id" INTEGER NOT NULL PRIMARY KEY);
                INSERT INTO "__highcool_write_probe" ("id") VALUES (1);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> SqliteTableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = $tableName;
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
