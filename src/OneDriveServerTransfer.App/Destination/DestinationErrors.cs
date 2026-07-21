namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Centralized builders for user-facing destination errors. Messages never contain
/// tenant IDs, drive IDs, employee identifiers, tokens, or protected database values.
/// Local destination paths may appear in protected logs but not in user-facing text.
/// </summary>
public static class DestinationErrors
{
    public static DestinationException InvalidDestinationPath(Exception? inner = null) => new(
        DestinationErrorCodes.InvalidDestinationPath,
        "The destination is not a valid local path",
        "The destination must be a full path on a local drive of this server, such as D:\\Archives.",
        "Enter a complete local path, including the drive letter, and try again.",
        inner);

    public static DestinationException NetworkDestination(Exception? inner = null) => new(
        DestinationErrorCodes.NetworkDestination,
        "Network destinations are not supported",
        "The destination is a network location. Only storage that is physically attached to this server can be used.",
        "Choose a folder on a local fixed drive of this server.",
        inner);

    public static DestinationException UnsupportedDriveType(Exception? inner = null) => new(
        DestinationErrorCodes.UnsupportedDriveType,
        "This drive type is not supported",
        "The destination drive is not fixed local storage. Removable, optical, RAM, and unknown drives cannot be used.",
        "Choose a folder on a local fixed drive of this server.",
        inner);

    public static DestinationException SystemDirectory(Exception? inner = null) => new(
        DestinationErrorCodes.SystemDirectory,
        "This location is protected by Windows",
        "The destination is inside a Windows system folder or the application installation folder, which cannot hold archive data.",
        "Choose a folder outside Windows system folders and the application installation folder.",
        inner);

    public static DestinationException UnsafeReparsePoint(Exception? inner = null) => new(
        DestinationErrorCodes.UnsafeReparsePoint,
        "The destination path is not safe",
        "The destination path passes through a link or junction that redirects to another location, which is not allowed for archive storage.",
        "Choose a direct folder path that does not use links or junctions.",
        inner);

    public static DestinationException PathTooLong(Exception? inner = null) => new(
        DestinationErrorCodes.PathTooLong,
        "A destination path is too long",
        "A file or folder name from the source would create a destination path longer than Windows supports, even with long-path handling.",
        "Choose a destination closer to the drive root, or shorten the source folder structure, and try again.",
        inner);

    public static DestinationException LayoutCreationFailed(Exception? inner = null) => new(
        DestinationErrorCodes.LayoutCreationFailed,
        "The destination folders could not be created",
        "The application could not create its working folders at the selected destination.",
        "Check that the destination drive is available and that you have permission to create folders there, then try again.",
        inner);

    public static DestinationException ForeignSourceBinding(Exception? inner = null) => new(
        DestinationErrorCodes.ForeignSourceBinding,
        "This destination belongs to a different archive",
        "The selected destination is already bound to a different employee OneDrive. It cannot be reused for another source.",
        "Choose a new empty destination, or sign in and select the source that this destination was created for.",
        inner);

    public static DestinationException InvalidStateDatabase(Exception? inner = null) => new(
        DestinationErrorCodes.InvalidStateDatabase,
        "The saved transfer state is not usable",
        "The transfer state stored at this destination is damaged or was created by a newer version of the application.",
        "Restore a known-good transfer state, or choose a new empty destination. Do not delete files manually.",
        inner);

    public static DestinationException NonEmptyDestinationWithoutState(Exception? inner = null) => new(
        DestinationErrorCodes.NonEmptyDestinationWithoutState,
        "This destination is not empty",
        "The selected destination already contains files but has no valid transfer state, so the application cannot tell whether they belong to a previous archive.",
        "Choose a new empty destination, or restore the matching transfer state before trying again.",
        inner);

    public static DestinationException DestinationStateFailure(Exception? inner = null) => new(
        DestinationErrorCodes.DestinationStateFailure,
        "The transfer state could not be saved",
        "The application could not write its state at the destination.",
        "Check that the destination drive is available and not full, then try again.",
        inner);

    public static DestinationException DestinationLocked(Exception? inner = null) => new(
        DestinationErrorCodes.DestinationLocked,
        "This destination is already in use",
        "Another window or process on this server is already using the selected destination.",
        "Close the other window or process that uses this destination, then try again.",
        inner);

    public static DestinationException ContainmentViolation(Exception? inner = null) => new(
        DestinationErrorCodes.ContainmentViolation,
        "A destination path is not safe",
        "A path used during the operation would leave the protected archive folder, so the operation was stopped.",
        "Do not modify the destination folders while the application is running. Try again with a fresh scan.",
        inner);

    public static DestinationException UntrustedExistingFile(Exception? inner = null) => new(
        DestinationErrorCodes.UntrustedExistingFile,
        "An existing destination file is not safe to use",
        "A file already at the destination is a link or is shared with another location, so the application will not overwrite it.",
        "Do not place links or shared files in the destination. Remove the conflicting file manually if it does not belong to a previous archive.",
        inner);
}
