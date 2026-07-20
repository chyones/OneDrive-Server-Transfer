using Microsoft.Extensions.Options;

namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Validates authentication configuration at startup. Placeholder or malformed values
/// fail safely with clear messages; the application must not attempt sign-in with them.
/// </summary>
public sealed class AuthenticationOptionsValidator : IValidateOptions<AuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!IsGuid(options.TenantId))
        {
            failures.Add("Authentication:TenantId must be a GUID for the configured tenant.");
        }

        if (!IsGuid(options.ClientId))
        {
            failures.Add("Authentication:ClientId must be a GUID for the public-client application.");
        }

        foreach (var objectId in options.AuthorizedOperatorObjectIds)
        {
            if (!IsGuid(objectId))
            {
                failures.Add("Authentication:AuthorizedOperatorObjectIds entries must be account object-ID GUIDs.");
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.RedirectUri) ||
            !options.RedirectUri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Authentication:RedirectUri must use the approved http://localhost system-browser value.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsGuid(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty;
}
