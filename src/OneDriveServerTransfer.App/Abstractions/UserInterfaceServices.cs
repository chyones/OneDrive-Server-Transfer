namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Local-folder picker abstraction behind which the WPF folder dialog lives, so the
/// view model stays testable. Returning null means the operator cancelled; it is
/// never an error.
/// </summary>
public interface IFolderPickerService
{
    /// <summary>Shows the destination folder picker and returns the chosen path.</summary>
    string? PickFolder();
}

/// <summary>
/// Operating-system shell integration abstraction (open a folder in the file
/// explorer), so the view model stays testable and shell calls stay out of it.
/// </summary>
public interface IShellService
{
    /// <summary>
    /// Opens a local folder in the operating-system file explorer. Throws
    /// <see cref="UserInterfaceException" /> with a stable reference code on failure;
    /// the message never carries protected identifiers.
    /// </summary>
    void OpenFolder(string path);
}
