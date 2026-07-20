namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// Stable reference codes for user-facing source-resolution errors. Codes are part of
/// the user-support contract and must never be renumbered.
/// </summary>
public static class SourceResolutionErrorCodes
{
    public const string InvalidEmployeeInput = "SRC-INPUT-001";
    public const string MalformedSourceUrl = "SRC-INPUT-002";
    public const string UnsupportedSource = "SRC-INPUT-003";
    public const string EmployeeNotFound = "SRC-USER-404";
    public const string OneDriveNotProvisioned = "SRC-DRIVE-404";
    public const string SourceOutsideTenant = "SRC-TENANT-001";
    public const string OperatorLacksAccess = "SRC-ACCESS-403";
    public const string Throttled = "SRC-THROTTLE-001";
    public const string ServiceUnavailable = "SRC-UNAVAILABLE-001";
    public const string Cancelled = "SRC-CANCELLED-001";
    public const string InvalidConfiguration = "SRC-CONFIG-001";
    public const string UnexpectedResponse = "SRC-RESPONSE-001";
}
