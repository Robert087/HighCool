namespace ERP.Application.LocalData;

public sealed class LocalDatabaseOptions
{
    public const string SectionName = "LocalDatabase";

    public bool AllowDevelopmentReset { get; set; }
}
