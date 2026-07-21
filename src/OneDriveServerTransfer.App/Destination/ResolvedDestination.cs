using System.IO;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// A validated local destination with its fixed layout. Employee content belongs only
/// under <see cref="ContentRootPath" />; transfer state, reports, and logs belong only
/// under <see cref="StateRootPath" />. The API surface exposes both paths separately so
/// callers cannot mix the two areas.
/// </summary>
public sealed record ResolvedDestination
{
    public const string ContentDirectoryName = "OneDriveData";
    public const string StateDirectoryName = "_TransferReport";
    public const string StateDatabaseFileName = "TransferState.db";
    public const string LockFileName = "destination.lock";

    public ResolvedDestination(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        RootPath = rootPath;
        ContentRootPath = Path.Combine(rootPath, ContentDirectoryName);
        StateRootPath = Path.Combine(rootPath, StateDirectoryName);
        StateDatabasePath = Path.Combine(StateRootPath, StateDatabaseFileName);
        LockFilePath = Path.Combine(StateRootPath, LockFileName);
    }

    /// <summary>The canonical, validated destination root selected by the operator.</summary>
    public string RootPath { get; }

    /// <summary>The only directory that may hold employee content.</summary>
    public string ContentRootPath { get; }

    /// <summary>The only directory that may hold transfer state, reports, and logs.</summary>
    public string StateRootPath { get; }

    /// <summary>The application-owned SQLite operational state database (D-016).</summary>
    public string StateDatabasePath { get; }

    /// <summary>The OS-backed exclusive-lock file for this destination.</summary>
    public string LockFilePath { get; }
}
