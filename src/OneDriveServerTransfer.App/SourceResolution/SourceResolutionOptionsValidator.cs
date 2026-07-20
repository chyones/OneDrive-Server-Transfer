using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace OneDriveServerTransfer.SourceResolution;

/// <summary>
/// Validates source-resolution configuration at startup. Placeholder or malformed
/// tenant OneDrive host values fail safely.
/// </summary>
public sealed partial class SourceResolutionOptionsValidator : IValidateOptions<SourceResolutionOptions>
{
    public ValidateOptionsResult Validate(string? name, SourceResolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!TenantHostRegex().IsMatch(options.TenantOneDriveHost ?? string.Empty))
        {
            return ValidateOptionsResult.Fail(
                "SourceResolution:TenantOneDriveHost must be the tenant OneDrive host (for example contoso-my.sharepoint.com).");
        }

        return ValidateOptionsResult.Success;
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*-my\.sharepoint\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TenantHostRegex();
}
