using Microsoft.Extensions.Options;

namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Identity facts extracted from the sign-in result and the Graph operator profile.
/// Contains no tokens or raw claims.
/// </summary>
public sealed record OperatorClaims(
    string TokenTenantId,
    string HomeTenantId,
    string ObjectId,
    string? UserPrincipalName,
    string? IdentityProvider,
    IReadOnlyCollection<string> GrantedScopes);

public sealed record OperatorValidationResult(bool IsValid, string? FailureReferenceCode)
{
    public static OperatorValidationResult Success { get; } = new(true, null);

    public static OperatorValidationResult Failure(string referenceCode) => new(false, referenceCode);
}

public interface IOperatorValidator
{
    /// <summary>
    /// Validates the signed-in account against the configured tenant, guest-account
    /// rules, the optional authorized-operator object-ID allowlist, and the approved
    /// minimum scope. Display names and mutable UPNs are never used as the durable
    /// authorization identity.
    /// </summary>
    OperatorValidationResult Validate(OperatorClaims claims);
}

public sealed class OperatorValidator : IOperatorValidator
{
    private readonly IOptions<AuthenticationOptions> _options;

    public OperatorValidator(IOptions<AuthenticationOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public OperatorValidationResult Validate(OperatorClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var options = _options.Value;

        if (string.IsNullOrWhiteSpace(claims.ObjectId))
        {
            return OperatorValidationResult.Failure(AuthenticationErrorCodes.IdentityMismatch);
        }

        if (!string.Equals(claims.TokenTenantId, options.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return OperatorValidationResult.Failure(AuthenticationErrorCodes.TenantMismatch);
        }

        if (!string.Equals(claims.HomeTenantId, options.TenantId, StringComparison.OrdinalIgnoreCase) ||
            claims.UserPrincipalName?.Contains("#EXT#", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OperatorValidationResult.Failure(AuthenticationErrorCodes.GuestAccountRejected);
        }

        if (options.AuthorizedOperatorObjectIds.Length > 0 &&
            !options.AuthorizedOperatorObjectIds.Any(id => string.Equals(id, claims.ObjectId, StringComparison.OrdinalIgnoreCase)))
        {
            return OperatorValidationResult.Failure(AuthenticationErrorCodes.OperatorNotAuthorized);
        }

        if (!claims.GrantedScopes.Any(scope => string.Equals(scope, ApprovedScopes.OperatorRead, StringComparison.OrdinalIgnoreCase)))
        {
            return OperatorValidationResult.Failure(AuthenticationErrorCodes.RequiredScopeMissing);
        }

        return OperatorValidationResult.Success;
    }
}
