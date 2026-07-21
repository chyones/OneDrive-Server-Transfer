namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Stable reference codes for user-facing destination errors. Codes are part of the
/// user-support contract and must never be renumbered.
/// </summary>
public static class DestinationErrorCodes
{
    public const string InvalidDestinationPath = "DST-PATH-001";
    public const string NetworkDestination = "DST-PATH-002";
    public const string UnsupportedDriveType = "DST-PATH-003";
    public const string SystemDirectory = "DST-PATH-004";
    public const string UnsafeReparsePoint = "DST-PATH-005";
    public const string PathTooLong = "DST-PATH-006";
    public const string LayoutCreationFailed = "DST-LAYOUT-001";
    public const string ForeignSourceBinding = "DST-BIND-001";
    public const string InvalidStateDatabase = "DST-BIND-002";
    public const string NonEmptyDestinationWithoutState = "DST-BIND-003";
    public const string DestinationStateFailure = "DST-BIND-004";
    public const string DestinationLocked = "DST-LOCK-001";
    public const string ContainmentViolation = "DST-SAFE-001";
    public const string UntrustedExistingFile = "DST-SAFE-002";
}
