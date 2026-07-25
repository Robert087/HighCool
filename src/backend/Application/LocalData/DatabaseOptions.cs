namespace ERP.Application.LocalData;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = DatabaseProviderNames.SqlServer;

    public string SqliteFileName { get; set; } = "highcool.db";
}

public static class DatabaseProviderNames
{
    public const string SqlServer = "SqlServer";
    public const string Sqlite = "Sqlite";
}
