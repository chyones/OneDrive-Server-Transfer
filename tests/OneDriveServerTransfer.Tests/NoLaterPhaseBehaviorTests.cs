using System.Text.RegularExpressions;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Guards the M2 boundary: no employee or drive resolution, Graph inventory, transfer,
/// destination, resume, or report behavior may exist yet. The only approved Microsoft
/// Graph surface in M2 is the operator /me endpoint.
/// </summary>
public class NoLaterPhaseBehaviorTests
{
    private static IEnumerable<string> SourceFiles()
    {
        var sourceRoot = Path.Combine(TestRepository.Root, "src");
        return Directory
            .EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string AllSource() =>
        string.Join("\n", SourceFiles().Select(File.ReadAllText));

    [Fact]
    public void OnlyApprovedGraphEndpointIsReferenced()
    {
        var matches = Regex.Matches(AllSource(), @"https://graph\.microsoft\.com/[^\s""']+")
            .Select(match => match.Value)
            .Distinct()
            .ToArray();

        Assert.Equal(
            ["https://graph.microsoft.com/v1.0/me?$select=id,userPrincipalName,displayName"],
            matches);
    }

    [Theory]
    [InlineData("/users/")]
    [InlineData("/drives/")]
    [InlineData("/root/delta")]
    [InlineData("@odata.nextLink")]
    [InlineData("@microsoft.graph.downloadUrl")]
    [InlineData("TransferEngine")]
    [InlineData("ScanCommand")]
    [InlineData("StartCopy")]
    public void NoEmployeeResolutionOrTransferBehaviorExists(string forbidden)
    {
        Assert.False(
            AllSource().Contains(forbidden, StringComparison.Ordinal),
            $"M2 source must not contain '{forbidden}'.");
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
