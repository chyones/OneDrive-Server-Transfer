using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.SourceResolution;

public class EmployeeSourceResolverTests
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string TenantHost = "contoso-my.sharepoint.com";
    private const string EmployeeObjectId = "66666666-6666-6666-6666-666666666666";
    private const string Upn = "employee@contoso.com";
    private const string DriveId = "b!drive-id-value";

    private static readonly string DriveJson = $$"""
        {
          "id": "{{DriveId}}",
          "driveType": "business",
          "webUrl": "https://{{TenantHost}}/personal/employee_contoso_com",
          "owner": { "user": { "id": "{{EmployeeObjectId}}", "displayName": "Employee Example" } },
          "quota": { "total": 1000, "used": 250, "remaining": 750, "state": "normal" }
        }
        """;

    private static readonly string SiteJson = $$"""
        {
          "id": "{{TenantHost}},site-collection-guid,site-guid",
          "webUrl": "https://{{TenantHost}}/personal/employee_contoso_com",
          "isPersonalSite": true,
          "siteCollection": { "hostname": "{{TenantHost}}" }
        }
        """;

    private static EmployeeSourceResolver CreateResolver(
        FakeGraphRequestChannel channel,
        string tenantHost = TenantHost) =>
        new(
            channel,
            Options.Create(new SourceResolutionOptions { TenantOneDriveHost = tenantHost }),
            Options.Create(new AuthenticationOptions { TenantId = TenantId }),
            NullLogger<EmployeeSourceResolver>.Instance);

    private static FakeGraphRequestChannel DriveChannel() =>
        new()
        {
            Handler = (_, _, _) => Task.FromResult(FakeGraphRequestChannel.Json(DriveJson)),
        };

    [Fact]
    public async Task ValidUpnResolvesCompleteSource()
    {
        var channel = DriveChannel();
        var resolver = CreateResolver(channel);

        var source = await resolver.ResolveAsync(Upn, CancellationToken.None);

        Assert.Equal(TenantId, source.TenantId);
        Assert.Equal(EmployeeObjectId, source.UserObjectId);
        Assert.Equal(Upn, source.UserPrincipalName);
        Assert.Equal("Employee Example", source.DisplayName);
        Assert.Equal(DriveId, source.DriveId);
        Assert.Equal("business", source.DriveType);
        Assert.Equal("Employee Example", source.DriveOwnerDisplayName);
        Assert.Equal($"https://{TenantHost}/personal/employee_contoso_com", source.DriveWebUrl);
        Assert.Equal(1000, source.QuotaTotalBytes);
        Assert.Equal(250, source.QuotaUsedBytes);
        Assert.Equal(750, source.QuotaRemainingBytes);
        Assert.Equal(EmployeeSourceMode.Upn, source.Mode);
        Assert.True(source.IsTenantConfirmed);

        var request = Assert.Single(channel.Requests);
        Assert.Contains("/users/employee%40contoso.com/drive", request.Uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("$select=id,driveType,webUrl,owner,quota", request.Uri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidUpnIsRejectedWithoutGraphCall()
    {
        var channel = DriveChannel();
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync("not-a-upn", CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.InvalidEmployeeInput, exception.ReferenceCode);
        Assert.Empty(channel.Requests);
    }

    [Fact]
    public async Task UnknownUpnMapsToEmployeeNotFound()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromException<JsonDocument>(
                new GraphRequestException(404, "Request_ResourceNotFound", false, null, "Resource 'x' does not exist")),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.EmployeeNotFound, exception.ReferenceCode);
    }

    [Fact]
    public async Task NotProvisionedHintMapsToOneDriveNotProvisioned()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromException<JsonDocument>(
                new GraphRequestException(404, "ResourceNotFound", false, null, "OneDrive is not provisioned for this user")),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.OneDriveNotProvisioned, exception.ReferenceCode);
    }

    [Fact]
    public async Task ForbiddenMapsToOperatorLacksAccess()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromException<JsonDocument>(
                new GraphRequestException(403, "accessDenied", false, null, null)),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.OperatorLacksAccess, exception.ReferenceCode);
    }

    [Fact]
    public async Task NonBusinessDriveIsRejectedAsUnsupported()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(FakeGraphRequestChannel.Json(DriveJson.Replace(
                "\"business\"", "\"personal\"", StringComparison.Ordinal))),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.UnsupportedSource, exception.ReferenceCode);
    }

    [Fact]
    public async Task MissingOwnerIdentityIsUnexpectedResponse()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(FakeGraphRequestChannel.Json(
                """{"id":"b!x","driveType":"business","webUrl":"https://contoso-my.sharepoint.com/personal/x","owner":{"user":{}}}""")),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.UnexpectedResponse, exception.ReferenceCode);
    }

    [Fact]
    public async Task ValidOneDriveUrlResolvesThroughSiteAndDrive()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (uri, _, _) => Task.FromResult(FakeGraphRequestChannel.Json(
                uri.ToString().Contains(":/personal/", StringComparison.Ordinal) ? SiteJson : DriveJson)),
        };
        var resolver = CreateResolver(channel);

        var source = await resolver.ResolveAsync(
            $"https://{TenantHost}/personal/employee_contoso_com", CancellationToken.None);

        Assert.Equal(TenantId, source.TenantId);
        Assert.Equal(EmployeeObjectId, source.UserObjectId);
        Assert.Null(source.UserPrincipalName); // not available from approved URL-mode endpoints
        Assert.Equal("Employee Example", source.DisplayName);
        Assert.Equal(DriveId, source.DriveId);
        Assert.Equal(EmployeeSourceMode.OneDriveRootUrl, source.Mode);
        Assert.True(source.IsTenantConfirmed);

        Assert.Equal(2, channel.Requests.Count);
        Assert.Contains($":/personal/employee_contoso_com", channel.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("$select=id,webUrl,isPersonalSite,siteCollection", channel.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("/drive?$select=id,driveType,webUrl,owner,quota", channel.Requests[1].Uri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UrlOnForeignHostIsRejectedAsTenantMismatch()
    {
        var channel = DriveChannel();
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(
                "https://othercorp-my.sharepoint.com/personal/employee_othercorp_com", CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.SourceOutsideTenant, exception.ReferenceCode);
        Assert.Empty(channel.Requests);
    }

    [Fact]
    public async Task NonPersonalSiteIsRejected()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(FakeGraphRequestChannel.Json(
                SiteJson.Replace("\"isPersonalSite\": true", "\"isPersonalSite\": false", StringComparison.Ordinal))),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync($"https://{TenantHost}/personal/employee_contoso_com", CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.UnsupportedSource, exception.ReferenceCode);
    }

    [Fact]
    public async Task ForeignSiteCollectionIsRejected()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromResult(FakeGraphRequestChannel.Json($$"""
                {
                  "id": "{{TenantHost}},site-collection-guid,site-guid",
                  "webUrl": "https://{{TenantHost}}/personal/employee_contoso_com",
                  "isPersonalSite": true,
                  "siteCollection": { "hostname": "othercorp-my.sharepoint.com" }
                }
                """)),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync($"https://{TenantHost}/personal/employee_contoso_com", CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.UnsupportedSource, exception.ReferenceCode);
    }

    [Fact]
    public async Task ThrottledMapsToThrottleError()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromException<JsonDocument>(
                new GraphRequestException(429, "tooManyRequests", true, TimeSpan.FromSeconds(5), null)),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.Throttled, exception.ReferenceCode);
    }

    [Fact]
    public async Task ExhaustedTransientFailureMapsToUnavailable()
    {
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, _) => Task.FromException<JsonDocument>(
                new GraphRequestException(503, "serviceUnavailable", true, null, null)),
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.ServiceUnavailable, exception.ReferenceCode);
    }

    [Fact]
    public async Task CancellationMapsToCancelledError()
    {
        using var cts = new CancellationTokenSource();
        var channel = new FakeGraphRequestChannel
        {
            Handler = (_, _, ct) =>
            {
                cts.Cancel();
                return Task.FromException<JsonDocument>(new OperationCanceledException(ct));
            },
        };
        var resolver = CreateResolver(channel);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync(Upn, cts.Token));

        Assert.Equal(SourceResolutionErrorCodes.Cancelled, exception.ReferenceCode);
    }

    [Fact]
    public async Task InvalidConfigurationFailsSafely()
    {
        var channel = DriveChannel();
        var resolver = new EmployeeSourceResolver(
            channel,
            new ValidatingSourceResolutionOptions(
                new SourceResolutionOptions { TenantOneDriveHost = "CONFIGURE_TENANT_ONEDRIVE_HOST" }),
            Options.Create(new AuthenticationOptions { TenantId = TenantId }),
            NullLogger<EmployeeSourceResolver>.Instance);

        var exception = await Assert.ThrowsAsync<SourceResolutionException>(
            () => resolver.ResolveAsync($"https://{TenantHost}/personal/employee_contoso_com", CancellationToken.None));

        Assert.Equal(SourceResolutionErrorCodes.InvalidConfiguration, exception.ReferenceCode);
        Assert.Empty(channel.Requests);
    }
}
