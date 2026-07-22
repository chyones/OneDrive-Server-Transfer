using System.Text.RegularExpressions;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Guards the milestone boundary: source code may use only the approved v1.0 endpoints
/// in GraphEndpoints.cs, and no report or later-phase UI behavior may exist yet. M5
/// download, transfer, verification, and reconciliation behavior exists and is covered
/// by its own tests; reports and Start Copy UI wiring remain prohibited.
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
        Assert.Contains("/drives/{0}/items/{1}", inventory, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("StartCopy")]
    [InlineData("ScanCommand")]
    [InlineData("GetDriveItemContent")]
    [InlineData("TransferReport.csv")]
    [InlineData("TransferSummary.json")]
    [InlineData("FailedFiles.csv")]
    [InlineData("CsvReportWriter")]
    public void NoReportOrLaterUiBehaviorExists(string forbidden)
    {
        Assert.False(
            AllSource().Contains(forbidden, StringComparison.Ordinal),
            $"M5 slice-2 source must not contain '{forbidden}'.");
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
