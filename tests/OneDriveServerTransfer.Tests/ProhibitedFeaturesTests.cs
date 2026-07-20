using System.Text.RegularExpressions;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Guards the binding contract's prohibited authentication flows, permissions, and API
/// surfaces. The scan covers application source and build inputs; the same patterns are
/// enforced in Windows CI by scripts/Test-ProhibitedContent.ps1.
/// </summary>
public class ProhibitedFeaturesTests
{
    // Patterns are defined as data for the scan. They exist here, in the test project,
    // which is intentionally outside the scanned source set.
    private static readonly string[] ProhibitedPatterns =
    [
        @"graph\.microsoft\.com/beta",
        @"Microsoft\.Graph\.Beta",
        "AcquireTokenByUsernamePassword",
        "AcquireTokenWithDeviceCode",
        "AcquireTokenForClient",
        "ConfidentialClientApplication",
        "ClientSecret",
        "X509Certificate",
        "PasswordBox",
        @"Files\.ReadWrite",
        @"Sites\.ReadWrite",
        @"Mail\.ReadWrite",
        @"Directory\.ReadWrite",
        "ROPC",
        "DeviceCode",
    ];

    private static IEnumerable<string> ScannedFiles()
    {
        var sourceRoot = Path.Combine(TestRepository.Root, "src");
        var files = Directory
            .EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var file in files)
        {
            yield return file;
        }

        yield return Path.Combine(TestRepository.Root, "OneDriveServerTransfer.sln");
        yield return Path.Combine(TestRepository.Root, "Directory.Build.props");
        yield return Path.Combine(TestRepository.Root, "Directory.Packages.props");
    }

    [Fact]
    public void SourceContainsNoProhibitedAuthenticationOrApiPatterns()
    {
        var violations = new List<string>();

        foreach (var file in ScannedFiles())
        {
            var content = File.ReadAllText(file);
            foreach (var pattern in ProhibitedPatterns)
            {
                if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                {
                    violations.Add($"{file}: {pattern}");
                }
            }
        }

        Assert.True(violations.Count == 0, "Prohibited patterns found: " + string.Join("; ", violations));
    }

    [Fact]
    public void SolutionReferencesMsalButNoGraphSdkPackages()
    {
        // MSAL is the approved M2 authentication library; the Graph SDK is not adopted.
        var lockFile = Path.Combine(
            TestRepository.Root, "src", "OneDriveServerTransfer.App", "packages.lock.json");

        if (!File.Exists(lockFile))
        {
            return; // Covered by ProjectStructureTests once restore has run.
        }

        var content = File.ReadAllText(lockFile);
        Assert.Contains("Microsoft.Identity.Client", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Graph", content, StringComparison.Ordinal);
    }

    [Fact]
    public void NoEmployeePasswordSurfaceExists()
    {
        var sourceRoot = Path.Combine(TestRepository.Root, "src");
        var sourceFiles = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(sourceRoot, "*.xaml", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("PasswordBox", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("EmployeePassword", content, StringComparison.OrdinalIgnoreCase);
        }
    }
}
