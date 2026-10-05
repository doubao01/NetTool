namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

public interface IAppLauncherService
{
    void LaunchApp(string appPath, string? arguments = null, bool admin = false);
    void OpenFile(string filePath);
    void OpenUrl(string url);
    void OpenFolder(string path);
}

public class AppLauncherService : IAppLauncherService
{
    public void LaunchApp(string appPath, string? arguments = null, bool admin = false)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = appPath,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true
        };

        if (admin)
        {
            psi.Verb = "runas";
        }

        System.Diagnostics.Process.Start(psi);
    }

    public void OpenFile(string filePath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(psi);
    }

    public void OpenUrl(string url)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(psi);
    }

    public void OpenFolder(string path)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = path,
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(psi);
    }
}
