namespace ERP.Infrastructure.LocalData;

internal static class FilePermissionTools
{
    public static void RestrictToCurrentUser(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = Directory.Exists(path)
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite;

            File.SetUnixFileMode(path, mode);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }
}
