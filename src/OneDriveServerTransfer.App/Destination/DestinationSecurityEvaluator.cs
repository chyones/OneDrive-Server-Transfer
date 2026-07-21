using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace OneDriveServerTransfer.Destination;

public enum DestinationSecurityVerdict
{
    Clear,
    BroadExposureWarning
}

/// <summary>A platform-neutral snapshot of one access-control entry, for evaluation.</summary>
public sealed record DestinationAclEntrySnapshot(
    string SecurityIdentifier,
    FileSystemRights Rights,
    bool IsAllow,
    bool IsInherited);

public sealed record DestinationSecurityFinding(string Path, string SecurityIdentifier, string Rights);

public sealed record DestinationSecurityAssessment(
    DestinationSecurityVerdict Verdict,
    IReadOnlyList<DestinationSecurityFinding> Findings)
{
    public static DestinationSecurityAssessment Clear { get; } =
        new(DestinationSecurityVerdict.Clear, []);
}

/// <summary>
/// Reads the access-control entries of a directory. Implemented on Windows through the
/// NTFS ACL APIs; off Windows there are no NTFS ACLs to read, so the reader returns no
/// entries and the evaluation is a no-op (real evaluation is Windows-runtime-verified).
/// </summary>
internal interface IDirectoryAclReader
{
    IReadOnlyList<DestinationAclEntrySnapshot> ReadEntries(string directoryPath);
}

/// <summary>
/// Warn-or-fail evaluation of destination NTFS permissions: employee archive data must
/// not be exposed broadly. The evaluation logic is platform-agnostic and unit-tested;
/// only the ACL reading is Windows-specific. Findings are warnings for the scan and
/// reports; the application never weakens or changes ACLs itself.
/// </summary>
public interface IDestinationSecurityEvaluator
{
    DestinationSecurityAssessment Evaluate(ResolvedDestination destination);
}

public sealed class DestinationSecurityEvaluator : IDestinationSecurityEvaluator
{
    private readonly IDirectoryAclReader _aclReader;
    private readonly ILogger<DestinationSecurityEvaluator> _logger;

    public DestinationSecurityEvaluator(ILogger<DestinationSecurityEvaluator> logger)
        : this(new WindowsDirectoryAclReader(), logger)
    {
    }

    internal DestinationSecurityEvaluator(
        IDirectoryAclReader aclReader, ILogger<DestinationSecurityEvaluator> logger)
    {
        _aclReader = aclReader ?? throw new ArgumentNullException(nameof(aclReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DestinationSecurityAssessment Evaluate(ResolvedDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var findings = new List<DestinationSecurityFinding>();
        foreach (var path in new[] { destination.ContentRootPath, destination.StateRootPath })
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            findings.AddRange(DestinationAclEvaluation.EvaluateEntries(path, _aclReader.ReadEntries(path)));
        }

        if (findings.Count == 0)
        {
            return DestinationSecurityAssessment.Clear;
        }

        _logger.LogWarning(
            "Destination permissions expose archive data broadly; findingCount={FindingCount}; path={DestinationPath}",
            findings.Count, destination.RootPath);
        return new DestinationSecurityAssessment(DestinationSecurityVerdict.BroadExposureWarning, findings);
    }
}

/// <summary>
/// Pure broad-exposure logic over ACL entry snapshots. An entry is a broad exposure
/// when it is an Allow rule for Everyone, Authenticated Users, or the built-in Users
/// group granting any data read or write right.
/// </summary>
internal static class DestinationAclEvaluation
{
    private static readonly string[] BroadSids =
    [
        "S-1-1-0",     // Everyone
        "S-1-5-11",    // Authenticated Users
        "S-1-5-32-545" // BUILTIN\Users
    ];

    /// <summary>
    /// Atomic data read/write rights. Composite rights (Read, Write, Modify,
    /// FullControl, ReadAndExecute) all include these bits, so composites are covered
    /// transitively, while non-data rights such as Synchronize or ReadAttributes are
    /// correctly not treated as broad exposure.
    /// </summary>
    private const FileSystemRights BroadRights =
        FileSystemRights.ReadData |
        FileSystemRights.WriteData |
        FileSystemRights.AppendData |
        FileSystemRights.CreateFiles |
        FileSystemRights.CreateDirectories;

    internal static IReadOnlyList<DestinationSecurityFinding> EvaluateEntries(
        string path, IEnumerable<DestinationAclEntrySnapshot> entries)
    {
        var findings = new List<DestinationSecurityFinding>();

        foreach (var entry in entries)
        {
            if (!entry.IsAllow)
            {
                continue;
            }

            if (!BroadSids.Contains(entry.SecurityIdentifier, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if ((entry.Rights & BroadRights) == 0)
            {
                continue;
            }

            findings.Add(new DestinationSecurityFinding(
                path, entry.SecurityIdentifier, entry.Rights.ToString()));
        }

        return findings;
    }
}

internal sealed class WindowsDirectoryAclReader : IDirectoryAclReader
{
    public IReadOnlyList<DestinationAclEntrySnapshot> ReadEntries(string directoryPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            var security = new DirectoryInfo(directoryPath).GetAccessControl();
            var rules = security.GetAccessRules(
                includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));

            var entries = new List<DestinationAclEntrySnapshot>();
            foreach (FileSystemAccessRule rule in rules)
            {
                entries.Add(new DestinationAclEntrySnapshot(
                    rule.IdentityReference.Value,
                    rule.FileSystemRights,
                    rule.AccessControlType == AccessControlType.Allow,
                    rule.IsInherited));
            }

            return entries;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or PrivilegeNotHeldException)
        {
            // An unreadable ACL must not fail the operation; it simply yields no evaluation.
            return [];
        }
    }
}
