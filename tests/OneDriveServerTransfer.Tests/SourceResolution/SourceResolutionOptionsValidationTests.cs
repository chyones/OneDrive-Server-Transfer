using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Tests.SourceResolution;

public class SourceResolutionOptionsValidationTests
{
    private readonly SourceResolutionOptionsValidator _validator = new();

    [Theory]
    [InlineData("contoso-my.sharepoint.com")]
    [InlineData("contoso-admin2-my.sharepoint.com")]
    public void ValidTenantOneDriveHostPasses(string host)
    {
        var result = _validator.Validate(null, new SourceResolutionOptions { TenantOneDriveHost = host });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CONFIGURE_TENANT_ONEDRIVE_HOST")]
    [InlineData("contoso.sharepoint.com")]
    [InlineData("https://contoso-my.sharepoint.com")]
    [InlineData("contoso-my.sharepoint.com/")]
    [InlineData("onedrive.live.com")]
    public void InvalidTenantOneDriveHostFails(string host)
    {
        var result = _validator.Validate(null, new SourceResolutionOptions { TenantOneDriveHost = host });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], message => message.Contains("TenantOneDriveHost"));
    }
}
