using Microsoft.Win32;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Shell;

/// <summary>
/// WPF implementation of the destination folder picker. Only the local folder dialog
/// is used; no shell namespaces beyond it. Constructing it never touches a window, so
/// dependency-injection resolution stays cheap and platform-safe.
/// </summary>
public sealed class WpfFolderPickerService : IFolderPickerService
{
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the local archive destination",
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
