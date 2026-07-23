namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Stable reference codes for user-facing shell errors. Codes are part of the
/// user-support contract and must never be renumbered.
/// </summary>
public static class UserInterfaceErrorCodes
{
    public const string Unexpected = "UI-GEN-001";
    public const string ShellOpenFailed = "UI-SHELL-001";
}

/// <summary>
/// A user-facing shell error with a short title, plain-language explanation,
/// corrective action, and stable reference code. It never carries passwords, tokens,
/// temporary URLs, raw service responses, tenant IDs, drive IDs, protected database
/// values, or stack traces in its user-facing fields.
/// </summary>
public sealed class UserInterfaceException : Exception
{
    public UserInterfaceException(
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

/// <summary>Centralized builders for user-facing shell errors.</summary>
public static class UserInterfaceErrors
{
    public static UserInterfaceException Unexpected(Exception? inner = null) => new(
        UserInterfaceErrorCodes.Unexpected,
        "Something went wrong",
        "An unexpected error occurred in the application.",
        "Try the operation again. If the problem continues, contact IT support and quote the reference code.",
        inner);

    public static UserInterfaceException ShellOpenFailed(Exception? inner = null) => new(
        UserInterfaceErrorCodes.ShellOpenFailed,
        "The folder could not be opened",
        "Windows could not open the folder in File Explorer. It may have been moved, removed, or not created yet.",
        "Check that the folder still exists, then try again.",
        inner);
}
