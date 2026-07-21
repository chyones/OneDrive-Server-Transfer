using System.Security.AccessControl;
using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Broad-exposure NTFS evaluation logic: Everyone, Authenticated Users, and built-in
/// Users Allow entries with data read or write rights produce warnings; specific
/// accounts, Deny entries, and non-data rights do not. ACL reading itself is
/// Windows-runtime behavior; the evaluation logic here is platform-agnostic.
/// </summary>
public class DestinationSecurityEvaluatorTests
{
    private const string Everyone = "S-1-1-0";
    private const string AuthenticatedUsers = "S-1-5-11";
    private const string BuiltinUsers = "S-1-5-32-545";
    private const string SpecificUser = "S-1-5-21-1000";

    [Theory]
    [InlineData(Everyone, FileSystemRights.Read)]
    [InlineData(Everyone, FileSystemRights.ReadData)]
    [InlineData(AuthenticatedUsers, FileSystemRights.Write)]
    [InlineData(AuthenticatedUsers, FileSystemRights.Modify)]
    [InlineData(BuiltinUsers, FileSystemRights.FullControl)]
    [InlineData(BuiltinUsers, FileSystemRights.CreateFiles)]
    [InlineData(Everyone, FileSystemRights.ReadAndExecute)]
    public void BroadAllowEntriesProduceFindings(string sid, FileSystemRights rights)
    {
        var entries = new[] { new DestinationAclEntrySnapshot(sid, rights, IsAllow: true, IsInherited: false) };

        var findings = DestinationAclEvaluation.EvaluateEntries("/dest", entries);

        var finding = Assert.Single(findings);
        Assert.Equal("/dest", finding.Path);
        Assert.Equal(sid, finding.SecurityIdentifier);
    }

    [Theory]
    [InlineData(SpecificUser, FileSystemRights.FullControl, true)]
    [InlineData(Everyone, FileSystemRights.FullControl, false)]
    [InlineData(Everyone, FileSystemRights.ReadAttributes, true)]
    [InlineData(AuthenticatedUsers, FileSystemRights.Synchronize, true)]
    public void NonBroadEntriesProduceNoFindings(string sid, FileSystemRights rights, bool isAllow)
    {
        var entries = new[] { new DestinationAclEntrySnapshot(sid, rights, isAllow, IsInherited: false) };

        Assert.Empty(DestinationAclEvaluation.EvaluateEntries("/dest", entries));
    }

    [Fact]
    public void EvaluatorReturnsWarningVerdictWithFindings()
    {
        var reader = new FakeDirectoryAclReader();
        reader.Entries.Add(new DestinationAclEntrySnapshot(
            Everyone, FileSystemRights.Read, IsAllow: true, IsInherited: true));
        var evaluator = new DestinationSecurityEvaluator(
            reader, NullLogger<DestinationSecurityEvaluator>.Instance);
        var root = Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

        try
        {
            var destination = new DestinationLayoutService(
                NullLogger<DestinationLayoutService>.Instance).EnsureLayout(root);

            var assessment = evaluator.Evaluate(destination);

            Assert.Equal(DestinationSecurityVerdict.BroadExposureWarning, assessment.Verdict);
            Assert.NotEmpty(assessment.Findings);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void EvaluatorReturnsClearVerdictWithoutBroadEntries()
    {
        var evaluator = new DestinationSecurityEvaluator(
            new FakeDirectoryAclReader(), NullLogger<DestinationSecurityEvaluator>.Instance);
        var root = Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

        try
        {
            var destination = new DestinationLayoutService(
                NullLogger<DestinationLayoutService>.Instance).EnsureLayout(root);

            var assessment = evaluator.Evaluate(destination);

            Assert.Equal(DestinationSecurityVerdict.Clear, assessment.Verdict);
            Assert.Empty(assessment.Findings);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void AclReaderReturnsNoEntriesOffWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Real NTFS reading is verified on the Windows runtime.
        }

        var reader = new WindowsDirectoryAclReader();

        Assert.Empty(reader.ReadEntries(Path.GetTempPath()));
    }
}
