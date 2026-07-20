using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class OperatorValidatorTests
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string OtherTenantId = "44444444-4444-4444-4444-444444444444";
    private const string ObjectId = "33333333-3333-3333-3333-333333333333";

    private static OperatorValidator CreateValidator(params string[] allowlist) => new(
        Options.Create(new AuthenticationOptions
        {
            TenantId = TenantId,
            ClientId = "22222222-2222-2222-2222-222222222222",
            AuthorizedOperatorObjectIds = allowlist,
        }));

    private static OperatorClaims ValidClaims() => new(
        TenantId,
        TenantId,
        ObjectId,
        "operator@example.test",
        null,
        ["User.Read", "openid"]);

    [Fact]
    public void MemberAccountInConfiguredTenantPasses()
    {
        var result = CreateValidator().Validate(ValidClaims());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void TokenFromAnotherTenantIsRejected()
    {
        var claims = ValidClaims() with { TokenTenantId = OtherTenantId };

        var result = CreateValidator().Validate(claims);

        Assert.False(result.IsValid);
        Assert.Equal(AuthenticationErrorCodes.TenantMismatch, result.FailureReferenceCode);
    }

    [Fact]
    public void AccountWithExternalHomeTenantIsRejectedAsGuest()
    {
        var claims = ValidClaims() with { HomeTenantId = OtherTenantId };

        var result = CreateValidator().Validate(claims);

        Assert.False(result.IsValid);
        Assert.Equal(AuthenticationErrorCodes.GuestAccountRejected, result.FailureReferenceCode);
    }

    [Fact]
    public void ExternalStyleUpnIsRejectedAsGuest()
    {
        var claims = ValidClaims() with { UserPrincipalName = "outsider_example.com#EXT#@example.test" };

        var result = CreateValidator().Validate(claims);

        Assert.False(result.IsValid);
        Assert.Equal(AuthenticationErrorCodes.GuestAccountRejected, result.FailureReferenceCode);
    }

    [Fact]
    public void AllowlistedOperatorPasses()
    {
        var result = CreateValidator(ObjectId).Validate(ValidClaims());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AllowlistMatchIsCaseInsensitiveGuidComparison()
    {
        var result = CreateValidator(ObjectId.ToUpperInvariant()).Validate(ValidClaims());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NonAllowlistedOperatorIsRejected()
    {
        var result = CreateValidator("55555555-5555-5555-5555-555555555555").Validate(ValidClaims());

        Assert.False(result.IsValid);
        Assert.Equal(AuthenticationErrorCodes.OperatorNotAuthorized, result.FailureReferenceCode);
    }

    [Fact]
    public void EmptyAllowlistPermitsAnyMemberAccount()
    {
        var result = CreateValidator().Validate(ValidClaims());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingObjectIdIsRejected()
    {
        var claims = ValidClaims() with { ObjectId = "" };

        var result = CreateValidator().Validate(claims);

        Assert.False(result.IsValid);
        Assert.Equal(AuthenticationErrorCodes.IdentityMismatch, result.FailureReferenceCode);
    }

    [Fact]
    public void MissingOperatorReadScopeIsRejected()
    {
        var claims = ValidClaims() with { GrantedScopes = ["openid", "profile"] };

        var result = CreateValidator().Validate(claims);

        Assert.False(result.IsValid);
        Assert.Equal(AuthenticationErrorCodes.RequiredScopeMissing, result.FailureReferenceCode);
    }
}
