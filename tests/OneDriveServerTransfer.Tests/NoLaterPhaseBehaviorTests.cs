using System.Text.RegularExpressions;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Guards the milestone boundary: source code may use only the approved v1.0 endpoints
/// in GraphEndpoints.cs, and no download, transfer, report, or later-phase UI behavior
/// may exist yet. M4 destination and source binding and the M5 scan, delta inventory,
/// and transfer state exist and are covered by their own tests.
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
        Assert.Contains("/drives/{0}/root/delta", inventory, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("@microsoft.graph.downloadUrl")]
    [InlineData("ScanCommand")]
    [InlineData("StartCopy")]
    [InlineData("TransferEngine")]
    [InlineData("GetDriveItemContent")]
    public void NoDownloadTransferReportOrLaterUiBehaviorExists(string forbidden)
    {
        Assert.False(
            AllSource().Contains(forbidden, StringComparison.Ordinal),
            $"M5 slice-1 source must not contain '{forbidden}'.");
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
