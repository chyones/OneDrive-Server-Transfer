namespace OneDriveServerTransfer.Inventory;

/// <summary>
/// Stable reference codes for inventory errors. Codes are part of the user-support
/// contract and must never be renumbered.
/// </summary>
public static class InventoryErrorCodes
{
    public const string MalformedDeltaResponse = "INV-DELTA-001";
}

/// <summary>
/// A user-facing inventory error with a short title, plain-language explanation,
/// corrective action, and stable reference code. It never carries tenant IDs, drive
/// IDs, tokens, paging URLs, or raw Graph responses in its user-facing fields.
/// </summary>
public sealed class InventoryException : Exception
{
    public InventoryException(
        string referenceCode,
        string title,
        string explanation,
        string correctiveAction,
        Exception? innerException = null)
        : base($"{referenceCode}: {title}", innerException)
    {
        ReferenceCode = referenceCode;
        Title = title;
        Explanation = explanation;
        CorrectiveAction = correctiveAction;
    }

    public string ReferenceCode { get; }

    public string Title { get; }

    public string Explanation { get; }

    public string CorrectiveAction { get; }
}

/// <summary>Centralized builders for user-facing inventory errors.</summary>
public static class InventoryErrors
{
    public static InventoryException MalformedDeltaResponse(Exception? inner = null) => new(
        InventoryErrorCodes.MalformedDeltaResponse,
        "The source inventory could not be read",
        "Microsoft returned an inventory response the application cannot safely interpret, so the scan was stopped before any state was trusted.",
        "Try the scan again. If the problem persists, contact IT support and quote the reference code.",
        inner);
}

/// <summary>
/// The delta checkpoint was invalidated by Microsoft (HTTP 410 with a supported reset
/// <c>Location</c>). Carries the opaque fresh-enumeration URL for the caller; the URL
/// is never logged or displayed. A 410 is never evidence of database corruption: state,
/// binding, local hashes, and archive content must be preserved.
/// </summary>
public sealed class DeltaCheckpointResetException : Exception
{
    public DeltaCheckpointResetException(Uri freshEnumerationLocation)
        : base("Microsoft invalidated the saved delta checkpoint and returned a fresh enumeration location.")
    {
        FreshEnumerationLocation = freshEnumerationLocation ?? throw new ArgumentNullException(nameof(freshEnumerationLocation));
    }

    /// <summary>The opaque fresh-enumeration URL (GRAPH-DELTA-003). Never log or persist as a token substitute.</summary>
    public Uri FreshEnumerationLocation { get; }
}
