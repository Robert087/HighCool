using ERP.Application.LocalData;
using ERP.Domain.System;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ERP.Infrastructure.LocalData;

public sealed class ApplicationDatabaseMetadataService(AppDbContext dbContext) : IApplicationDatabaseMetadataService
{
    private static readonly Guid MetadataId = Guid.Parse("5e5df3c5-a35e-4b75-8e2b-9ecb4c61d221");

    public async Task<DatabaseMetadataDto> EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlite())
        {
            await EnsureSqliteMetadataTableAsync(cancellationToken);
        }

        var metadata = await dbContext.ApplicationDatabaseMetadata
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == MetadataId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var applicationVersion = GetApplicationVersion();

        if (metadata is null)
        {
            metadata = new ApplicationDatabaseMetadata(MetadataId)
            {
                ApplicationVersion = applicationVersion,
                InstallationId = Guid.NewGuid().ToString("N"),
                DatabaseSchemaVersion = DatabaseSchemaInfo.CurrentSchemaVersion,
                DatabaseCreatedAtUtc = utcNow,
                LastSuccessfulSchemaUpgradeAtUtc = utcNow,
                CreatedBy = "system"
            };

            dbContext.ApplicationDatabaseMetadata.Add(metadata);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Map(metadata);
        }

        if (metadata.DatabaseSchemaVersion > DatabaseSchemaInfo.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"The database schema version ({metadata.DatabaseSchemaVersion}) is newer than this HighCool build supports ({DatabaseSchemaInfo.CurrentSchemaVersion}). Start a compatible newer application version before using this database.");
        }

        if (string.IsNullOrWhiteSpace(metadata.InstallationId))
        {
            metadata.InstallationId = Guid.NewGuid().ToString("N");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!string.Equals(metadata.ApplicationVersion, applicationVersion, StringComparison.Ordinal))
        {
            metadata.ApplicationVersion = applicationVersion;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(metadata);
    }

    public async Task<DatabaseMetadataDto?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var metadata = await dbContext.ApplicationDatabaseMetadata
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == MetadataId, cancellationToken);

        return metadata is null ? null : Map(metadata);
    }

    private async Task EnsureSqliteMetadataTableAsync(CancellationToken cancellationToken)
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
                CREATE TABLE IF NOT EXISTS "application_database_metadata" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_application_database_metadata" PRIMARY KEY,
                    "application_version" TEXT NOT NULL,
                    "installation_id" TEXT NOT NULL DEFAULT '',
                    "database_schema_version" INTEGER NOT NULL,
                    "database_created_at_utc" TEXT NOT NULL,
                    "last_successful_schema_upgrade_at_utc" TEXT NOT NULL,
                    "created_at" TEXT NOT NULL,
                    "created_by" TEXT NOT NULL,
                    "updated_at" TEXT NULL,
                    "updated_by" TEXT NULL
                );
                """;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DatabaseMetadataDto Map(ApplicationDatabaseMetadata metadata)
        => new(
            metadata.ApplicationVersion,
            metadata.InstallationId,
            metadata.DatabaseSchemaVersion,
            metadata.DatabaseCreatedAtUtc,
            metadata.LastSuccessfulSchemaUpgradeAtUtc);

    private static string GetApplicationVersion()
        => typeof(AppDbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
           ?? typeof(AppDbContext).Assembly.GetName().Version?.ToString()
           ?? "0.0.0";

}
