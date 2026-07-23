using System.Diagnostics;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Shell;

/// <summary>
/// Windows shell integration: opens a local folder in File Explorer. The application
/// targets Windows Server only; on any other platform the call fails with the stable
/// reference-coded shell error instead of attempting a platform-specific open.
/// </summary>
public sealed class WindowsShellService : IShellService
{
    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!OperatingSystem.IsWindows())
        {
            throw UserInterfaceErrors.ShellOpenFailed();
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            throw UserInterfaceErrors.ShellOpenFailed(exception);
        }
    }
}
