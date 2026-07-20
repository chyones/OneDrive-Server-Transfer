using System.Text.RegularExpressions;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Guards the M3 boundary: employee source resolution may use only the approved v1.0
/// endpoints in GraphEndpoints.cs; no inventory, delta, download, transfer,
/// destination, or report behavior may exist yet.
/// </summary>
public class NoLaterPhaseBehaviorTests
{
    private static string SourceRoot => Path.Combine(TestRepository.Root, "src");

    private static IEnumerable<string> SourceFiles() =>
        Directory
            .EnumerateFiles(SourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string AllSource() =>
        string.Join("\n", SourceFiles().Select(File.ReadAllText));

    [Fact]
    public void GraphUrlsExistOnlyInTheApprovedEndpointInventory()
    {
        foreach (var file in SourceFiles())
        {
            var fileName = Path.GetFileName(file);
            var containsGraphUrl = File.ReadAllText(file).Contains("graph.microsoft.com", StringComparison.Ordinal);

            if (containsGraphUrl)
            {
                Assert.True(
                    fileName is "GraphEndpoints.cs",
                    $"Microsoft Graph URLs must live only in GraphEndpoints.cs; found one in {fileName}.");
            }
        }
    }

    [Fact]
    public void EndpointInventoryContainsOnlyApprovedV1Endpoints()
    {
        var inventory = File.ReadAllText(
            Path.Combine(SourceRoot, "OneDriveServerTransfer.App", "SourceResolution", "GraphEndpoints.cs"));

        // The only literal URL is the v1.0 base; every endpoint is composed from it.
        var literalUrls = Regex.Matches(inventory, @"https://graph\.microsoft\.com/[^\s""']+")
            .Select(match => match.Value.TrimEnd('"', ';'))
            .ToArray();

        Assert.Equal(["https://graph.microsoft.com/v1.0"], literalUrls);

        // Approved endpoint paths from the matrix, all composed from V1Base.
        Assert.Contains("/me?$select=id,userPrincipalName,displayName", inventory, StringComparison.Ordinal);
        Assert.Contains("/users/{0}/drive", inventory, StringComparison.Ordinal);
        Assert.Contains("/sites/{0}:/{1}", inventory, StringComparison.Ordinal);
        Assert.Contains("/sites/{0}/drive", inventory, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/drives/")]
    [InlineData("/root/delta")]
    [InlineData("@odata.nextLink")]
    [InlineData("@microsoft.graph.downloadUrl")]
    [InlineData("ScanCommand")]
    [InlineData("StartCopy")]
    [InlineData("TransferEngine")]
    [InlineData("GetDriveItemContent")]
    public void NoInventoryTransferDestinationOrReportBehaviorExists(string forbidden)
    {
        Assert.False(
            AllSource().Contains(forbidden, StringComparison.Ordinal),
            $"M3 source must not contain '{forbidden}'.");
    }

    [Fact]
    public void NoGraphSdkPackageIsReferenced()
    {
        var projectFile = Path.Combine(
            TestRepository.Root, "src", "OneDriveServerTransfer.App", "OneDriveServerTransfer.App.csproj");

        var content = File.ReadAllText(projectFile);
        Assert.DoesNotContain("Microsoft.Graph", content, StringComparison.Ordinal);
    }
}
