namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// Centralized builders for user-facing source-resolution errors. Messages never
/// contain employee identifiers, URLs, tokens, or protocol details.
/// </summary>
public static class SourceResolutionErrors
{
    public static SourceResolutionException InvalidEmployeeInput(Exception? inner = null) => new(
        SourceResolutionErrorCodes.InvalidEmployeeInput,
        "Enter a valid employee user principal name",
        "The employee entry is not a valid user principal name (for example, name@example.com).",
        "Correct the employee user principal name and try again.",
        inner);

    public static SourceResolutionException MalformedSourceUrl(Exception? inner = null) => new(
        SourceResolutionErrorCodes.MalformedSourceUrl,
        "The OneDrive address is not valid",
        "The entry looks like a web address but it is not a complete, valid HTTPS address.",
        "Paste the complete root address of the employee OneDrive, starting with https://.",
        inner);

    public static SourceResolutionException UnsupportedSource(Exception? inner = null) => new(
        SourceResolutionErrorCodes.UnsupportedSource,
        "This source is not supported",
        "The entry is not the root of an employee OneDrive for Business. Files, subfolders, shared links, consumer OneDrive, and SharePoint or Teams libraries are not supported.",
        "Enter the employee user principal name or the root address of the employee OneDrive for Business.",
        inner);

    public static SourceResolutionException EmployeeNotFound(Exception? inner = null) => new(
        SourceResolutionErrorCodes.EmployeeNotFound,
        "The employee was not found",
        "No account matching this entry exists in the configured organization.",
        "Check the spelling of the employee user principal name or OneDrive address and try again.",
        inner);

    public static SourceResolutionException OneDriveNotProvisioned(Exception? inner = null) => new(
        SourceResolutionErrorCodes.OneDriveNotProvisioned,
        "This employee has no OneDrive yet",
        "The employee account exists, but its OneDrive for Business has not been created.",
        "Ask the employee or an administrator to open OneDrive once so it is created, then try again.",
        inner);

    public static SourceResolutionException SourceOutsideTenant(Exception? inner = null) => new(
        SourceResolutionErrorCodes.SourceOutsideTenant,
        "This source is outside the configured organization",
        "The address belongs to a different organization than the one this application is configured for.",
        "Use an employee account or OneDrive address from the configured organization.",
        inner);

    public static SourceResolutionException OperatorLacksAccess(Exception? inner = null) => new(
        SourceResolutionErrorCodes.OperatorLacksAccess,
        "No access to this employee OneDrive",
        "The signed-in transfer account does not have access to this employee OneDrive.",
        "Ask an administrator to grant the transfer account access to the employee OneDrive, then try again.",
        inner);

    public static SourceResolutionException Throttled(Exception? inner = null) => new(
        SourceResolutionErrorCodes.Throttled,
        "The service is busy",
        "Microsoft 365 is limiting requests right now.",
        "Wait a few minutes and try again.",
        inner);

    public static SourceResolutionException ServiceUnavailable(Exception? inner = null) => new(
        SourceResolutionErrorCodes.ServiceUnavailable,
        "Microsoft 365 is unavailable",
        "The source resolution service could not be reached or did not respond.",
        "Check the network connection and try again.",
        inner);

    public static SourceResolutionException Cancelled(Exception? inner = null) => new(
        SourceResolutionErrorCodes.Cancelled,
        "Operation cancelled",
        "Source resolution was cancelled before it completed.",
        "Start the operation again when you are ready.",
        inner);

    public static SourceResolutionException InvalidConfiguration(Exception? inner = null) => new(
        SourceResolutionErrorCodes.InvalidConfiguration,
        "Source resolution is not configured",
        "The tenant OneDrive host setting is missing or invalid.",
        "Ask your administrator to provide the approved tenant OneDrive host in the local appsettings.json.",
        inner);

    public static SourceResolutionException UnexpectedResponse(Exception? inner = null) => new(
        SourceResolutionErrorCodes.UnexpectedResponse,
        "The source could not be confirmed",
        "Microsoft 365 returned an answer the application could not use safely.",
        "Try again. If the problem continues, contact your administrator and quote the reference code.",
        inner);
}
