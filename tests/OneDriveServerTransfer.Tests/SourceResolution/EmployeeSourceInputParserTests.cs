using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Tests.SourceResolution;

public class EmployeeSourceInputParserTests
{
    [Theory]
    [InlineData("user@contoso.com")]
    [InlineData("first.last+tag@contoso.onmicrosoft.com")]
    [InlineData("  user@contoso.com  ")]
    public void ValidUpnParses(string input)
    {
        var success = EmployeeSourceInputParser.TryParse(input, out var parsed, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(EmployeeSourceInputKind.Upn, parsed!.Kind);
        Assert.Equal(input.Trim(), parsed.Upn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("user")]
    [InlineData("user@")]
    [InlineData("@contoso.com")]
    [InlineData("a@b@c.com")]
    [InlineData("user@contoso")]
    [InlineData("user name@contoso.com")]
    [InlineData("user#EXT#@contoso.com")]
    [InlineData("outsider_contoso.com#EXT#@contoso.onmicrosoft.com")]
    public void InvalidUpnIsRejected(string input)
    {
        var success = EmployeeSourceInputParser.TryParse(input, out _, out var error);

        Assert.False(success);
        Assert.Equal(SourceResolutionErrorCodes.InvalidEmployeeInput, error!.ReferenceCode);
    }

    [Fact]
    public void OverlongUpnIsRejected()
    {
        var input = new string('a', 100) + "@" + new string('b', 100) + ".com";

        var success = EmployeeSourceInputParser.TryParse(input, out _, out var error);

        Assert.False(success);
        Assert.Equal(SourceResolutionErrorCodes.InvalidEmployeeInput, error!.ReferenceCode);
    }

    [Theory]
    [InlineData("https://contoso-my.sharepoint.com/personal/user_contoso_com")]
    [InlineData("https://contoso-my.sharepoint.com/personal/user_contoso_com/")]
    public void ValidOneDriveRootUrlParses(string input)
    {
        var success = EmployeeSourceInputParser.TryParse(input, out var parsed, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(EmployeeSourceInputKind.OneDriveRootUrl, parsed!.Kind);
        Assert.Equal("contoso-my.sharepoint.com", parsed.Url!.Host);
        Assert.Equal("personal/user_contoso_com", parsed.Url.SitePath);
        Assert.Equal("user_contoso_com", parsed.Url.AccountSegment);
    }

    [Theory]
    [InlineData("http://contoso-my.sharepoint.com/personal/user_contoso_com")]
    [InlineData("not a url at all ://")]
    public void MalformedUrlIsRejected(string input)
    {
        var success = EmployeeSourceInputParser.TryParse(input, out _, out var error);

        Assert.False(success);
        Assert.Equal(SourceResolutionErrorCodes.MalformedSourceUrl, error!.ReferenceCode);
    }

    [Theory]
    [InlineData("https://contoso-my.sharepoint.com")]
    [InlineData("https://contoso-my.sharepoint.com/")]
    [InlineData("https://contoso-my.sharepoint.com/personal/")]
    [InlineData("https://contoso-my.sharepoint.com/personal/user_contoso_com/Documents")]
    [InlineData("https://contoso-my.sharepoint.com/personal/user_contoso_com/Documents/file.docx")]
    [InlineData("https://contoso-my.sharepoint.com/:u:/g/personal/user_contoso_com/EaBcDeF")]
    [InlineData("https://contoso-my.sharepoint.com/personal/user_contoso_com/_layouts/15/viewlsts.aspx")]
    [InlineData("https://contoso-my.sharepoint.com/personal/user_contoso_com?web=1")]
    [InlineData("https://contoso-my.sharepoint.com/personal/user_contoso_com#fragment")]
    [InlineData("https://contoso.sharepoint.com/sites/team")]
    [InlineData("https://onedrive.live.com/?id=root")]
    [InlineData("https://contoso-my.sharepoint.com/teams/somechannel")]
    public void UnsupportedUrlIsRejected(string input)
    {
        var success = EmployeeSourceInputParser.TryParse(input, out _, out var error);

        Assert.False(success);
        Assert.Equal(SourceResolutionErrorCodes.UnsupportedSource, error!.ReferenceCode);
    }
}
