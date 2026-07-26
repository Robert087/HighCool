using Microsoft.Data.Sqlite;

namespace ERP.Application.Tests;

internal static class SqliteTestDatabase
{
    public static string CreateConnectionString(string databasePath)
        => new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

    public static void DeleteSqliteFileSet(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public static void DeleteDirectoryIfExists(string? directoryPath)
    {
        SqliteConnection.ClearAllPools();

        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
