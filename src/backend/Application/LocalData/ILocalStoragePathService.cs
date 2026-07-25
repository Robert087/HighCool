namespace ERP.Application.LocalData;

public interface ILocalStoragePathService
{
    string DataDirectory { get; }

    string BackupDirectory { get; }

    string PendingBackupDirectory { get; }

    string LogDirectory { get; }

    string GetSqliteDatabasePath(string fileName);

    void EnsureRequiredDirectories();
}
