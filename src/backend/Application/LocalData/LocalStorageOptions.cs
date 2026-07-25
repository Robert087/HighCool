namespace ERP.Application.LocalData;

public sealed class LocalStorageOptions
{
    public const string SectionName = "LocalStorage";

    public string? DataDirectory { get; set; }

    public string? BackupDirectory { get; set; }

    public string? PendingBackupDirectory { get; set; }

    public string? LogDirectory { get; set; }
}
