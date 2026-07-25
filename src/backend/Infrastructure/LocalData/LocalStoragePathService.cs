using ERP.Application.LocalData;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.LocalData;

public sealed class LocalStoragePathService : ILocalStoragePathService
{
    private const string ApplicationDirectoryName = "HighCool";
    private readonly IHostEnvironment _hostEnvironment;
    private readonly LocalStorageOptions _options;

    public LocalStoragePathService(IOptions<LocalStorageOptions> options, IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;

        var rootDirectory = ResolveDefaultRootDirectory();
        DataDirectory = ResolveDirectory(_options.DataDirectory, Path.Combine(rootDirectory, "Data"));
        BackupDirectory = ResolveDirectory(_options.BackupDirectory, Path.Combine(rootDirectory, "Backups"));
        PendingBackupDirectory = ResolveDirectory(_options.PendingBackupDirectory, Path.Combine(rootDirectory, "PendingBackups"));
        LogDirectory = ResolveDirectory(_options.LogDirectory, Path.Combine(rootDirectory, "Logs"));
    }

    public string DataDirectory { get; }

    public string BackupDirectory { get; }

    public string PendingBackupDirectory { get; }

    public string LogDirectory { get; }

    public string GetSqliteDatabasePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("Database:SqliteFileName must be configured when Database:Provider is Sqlite.");
        }

        if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Database:SqliteFileName must be a file name, not a path.");
        }

        return Path.GetFullPath(Path.Combine(DataDirectory, fileName));
    }

    public void EnsureRequiredDirectories()
    {
        EnsureDirectory(DataDirectory, nameof(DataDirectory));
        EnsureDirectory(BackupDirectory, nameof(BackupDirectory));
        EnsureDirectory(PendingBackupDirectory, nameof(PendingBackupDirectory));
        EnsureDirectory(LogDirectory, nameof(LogDirectory));
    }

    private string ResolveDefaultRootDirectory()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, ".highcool", "local"));
        }

        if (OperatingSystem.IsWindows())
        {
            var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(commonApplicationData))
            {
                return Path.GetFullPath(Path.Combine(commonApplicationData, ApplicationDirectoryName));
            }
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            return Path.GetFullPath(Path.Combine(localApplicationData, ApplicationDirectoryName));
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.GetFullPath(Path.Combine(userProfile, ".local", "share", ApplicationDirectoryName));
        }

        throw new InvalidOperationException("Could not resolve a stable operating-system application-data directory for HighCool.");
    }

    private string ResolveDirectory(string? configuredPath, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(defaultPath);
        }

        return Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configuredPath));
    }

    private static void EnsureDirectory(string directoryPath, string optionName)
    {
        if (File.Exists(directoryPath))
        {
            throw new InvalidOperationException($"LocalStorage:{optionName} points to a file. Configure a directory path.");
        }

        try
        {
            Directory.CreateDirectory(directoryPath);

            var probePath = Path.Combine(directoryPath, $".highcool-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new InvalidOperationException($"HighCool cannot create or write to local storage directory '{directoryPath}'. Check LocalStorage configuration and filesystem permissions.", exception);
        }
    }
}
