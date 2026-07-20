namespace OneDriveServerTransfer.ViewModels;

/// <summary>
/// View model for the single application window. M1 contains only the shell state; the
/// transfer workflow commands arrive in later milestones.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    public string WindowTitle => "OneDrive Server Transfer";

    public string StatusMessage =>
        "Milestone M1 solution foundation. Sign-in, employee resolution, scan, copy, " +
        "and reports are not implemented in this milestone.";
}
