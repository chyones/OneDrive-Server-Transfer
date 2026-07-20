using System.Text.Json;
using System.Xml.Linq;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Verifies the M1 solution structure required by the binding contract: root solution,
/// Windows-targeted WPF application project, automated-test project, deterministic
/// restore inputs, and the mandatory Windows CI workflow.
/// </summary>
public class ProjectStructureTests
{
    private static string AppProjectPath =>
        Path.Combine(TestRepository.Root, "src", "OneDriveServerTransfer.App", "OneDriveServerTransfer.App.csproj");

    private static string TestProjectPath =>
        Path.Combine(TestRepository.Root, "tests", "OneDriveServerTransfer.Tests", "OneDriveServerTransfer.Tests.csproj");

    [Fact]
    public void SolutionExistsAtRepositoryRootAndContainsBothProjects()
    {
        var solutionPath = Path.Combine(TestRepository.Root, "OneDriveServerTransfer.sln");

        Assert.True(File.Exists(solutionPath), "OneDriveServerTransfer.sln must exist at the repository root.");

        var solutionText = File.ReadAllText(solutionPath);
        Assert.Contains("OneDriveServerTransfer.App.csproj", solutionText);
        Assert.Contains("OneDriveServerTransfer.Tests.csproj", solutionText);
    }

    [Fact]
    public void AppProjectTargetsWindowsNet10WithWpf()
    {
        Assert.True(File.Exists(AppProjectPath), "The WPF application project must exist.");

        var project = XDocument.Load(AppProjectPath);
        var properties = project.Descendants();

        Assert.Contains(properties, e => e.Name.LocalName == "OutputType" && e.Value == "WinExe");
        Assert.Contains(properties, e => e.Name.LocalName == "TargetFramework" && e.Value == "net10.0-windows");
        Assert.Contains(properties, e => e.Name.LocalName == "UseWPF" && e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TestProjectTargetsWindowsNet10AndReferencesApp()
    {
        Assert.True(File.Exists(TestProjectPath), "The automated-test project must exist.");

        var project = XDocument.Load(TestProjectPath);

        Assert.Contains(project.Descendants(), e => e.Name.LocalName == "TargetFramework" && e.Value == "net10.0-windows");
        Assert.Contains(project.Descendants(), e =>
            e.Name.LocalName == "ProjectReference" &&
            (e.Attribute("Include")?.Value.Contains("OneDriveServerTransfer.App.csproj", StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public void DeterministicRestoreInputsExist()
    {
        Assert.True(
            File.Exists(Path.Combine(TestRepository.Root, "Directory.Packages.props")),
            "Central package management file must exist.");
        Assert.True(
            File.Exists(Path.Combine(TestRepository.Root, "global.json")),
            "global.json must pin the .NET SDK.");

        var appLock = Path.Combine(TestRepository.Root, "src", "OneDriveServerTransfer.App", "packages.lock.json");
        var testLock = Path.Combine(TestRepository.Root, "tests", "OneDriveServerTransfer.Tests", "packages.lock.json");
        Assert.True(File.Exists(appLock), "The application project must commit packages.lock.json.");
        Assert.True(File.Exists(testLock), "The test project must commit packages.lock.json.");

        var buildProps = File.ReadAllText(Path.Combine(TestRepository.Root, "Directory.Build.props"));
        Assert.Contains("RestorePackagesWithLockFile", buildProps);
    }

    [Fact]
    public void GlobalJsonPinsDotNet10Sdk()
    {
        var globalJsonPath = Path.Combine(TestRepository.Root, "global.json");
        using var document = JsonDocument.Parse(File.ReadAllText(globalJsonPath));

        var version = document.RootElement.GetProperty("sdk").GetProperty("version").GetString();
        Assert.NotNull(version);
        Assert.StartsWith("10.", version, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsCiWorkflowCoversRequiredChecks()
    {
        var workflowPath = Path.Combine(TestRepository.Root, ".github", "workflows", "windows-ci.yml");
        Assert.True(File.Exists(workflowPath), "The mandatory Windows CI workflow must exist.");

        var workflow = File.ReadAllText(workflowPath);
        Assert.Contains("windows-latest", workflow);
        Assert.Contains("--locked-mode", workflow);
        Assert.Contains("Release", workflow);
        Assert.Contains("dotnet test", workflow);
        Assert.Contains("--vulnerable", workflow);
        Assert.Contains("Test-ProhibitedContent.ps1", workflow);
        Assert.Contains("gitleaks", workflow);
    }

    [Fact]
    public void AppSettingsExampleExists()
    {
        var path = Path.Combine(TestRepository.Root, "src", "OneDriveServerTransfer.App", "appsettings.example.json");
        Assert.True(File.Exists(path), "appsettings.example.json must exist.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }
}
