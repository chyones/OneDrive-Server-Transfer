using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class AuthenticationOptionsValidationTests
{
    private readonly AuthenticationOptionsValidator _validator = new();

    private static AuthenticationOptions Valid() => new()
    {
        TenantId = "11111111-1111-1111-1111-111111111111",
        ClientId = "22222222-2222-2222-2222-222222222222",
        RedirectUri = "http://localhost",
    };

    [Fact]
    public void ValidConfigurationPasses()
    {
        Assert.True(_validator.Validate(null, Valid()).Succeeded);
    }

    [Fact]
    public void ValidConfigurationWithAllowlistPasses()
    {
        var options = Valid();
        options.AuthorizedOperatorObjectIds = ["33333333-3333-3333-3333-333333333333"];

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CONFIGURE_TENANT_ID")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void MissingOrPlaceholderTenantIdFails(string tenantId)
    {
        var options = Valid();
        options.TenantId = tenantId;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], message => message.Contains("TenantId"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("CONFIGURE_CLIENT_ID")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void MissingOrPlaceholderClientIdFails(string clientId)
    {
        var options = Valid();
        options.ClientId = clientId;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], message => message.Contains("ClientId"));
    }

    [Fact]
    public void NonGuidAllowlistEntryFails()
    {
        var options = Valid();
        options.AuthorizedOperatorObjectIds = ["not-a-guid"];

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], message => message.Contains("AuthorizedOperatorObjectIds"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.test/callback")]
    [InlineData("urn:ietf:wg:oauth:2.0:oob")]
    public void NonApprovedRedirectUriFails(string redirectUri)
    {
        var options = Valid();
        options.RedirectUri = redirectUri;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], message => message.Contains("RedirectUri"));
    }

    [Fact]
    public void LocalhostRedirectUriPasses()
    {
        var options = Valid();
        options.RedirectUri = "http://localhost";

        Assert.True(_validator.Validate(null, options).Succeeded);
    }
}
