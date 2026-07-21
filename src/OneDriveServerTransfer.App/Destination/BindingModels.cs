namespace OneDriveServerTransfer.Destination;

/// <summary>
/// The durable source identity a destination is bound to (D-018, D-032): tenant ID,
/// source drive ID, and employee Entra object ID. The normalized employee UPN is
/// display and audit data only and is never part of the binding comparison.
/// </summary>
public sealed record SourceBindingIdentity(
    string TenantId,
    string DriveId,
    string EmployeeObjectId,
    string? EmployeeUpn);

/// <summary>
/// The already-authorized signed-in transfer operator. Recorded for audit only; the
/// operator identity is never part of the destination binding (D-032). Authorization
/// itself is enforced during sign-in (tenant and optional allowlist checks, M2) before
/// this identity reaches the destination layer.
/// </summary>
public sealed record OperatorIdentity(string ObjectId, string? UserPrincipalName);

/// <summary>How a destination open concluded.</summary>
public enum DestinationBindingOutcome
{
    /// <summary>The destination was empty and is now bound to the current source.</summary>
    BoundNew,

    /// <summary>The destination was already bound to the same source and passed all checks.</summary>
    ResumedExisting
}

public sealed record DestinationBindingResult(DestinationBindingOutcome Outcome);
