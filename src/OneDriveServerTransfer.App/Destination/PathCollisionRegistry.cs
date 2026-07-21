namespace OneDriveServerTransfer.Destination;

public enum MappedItemKind
{
    File,
    Folder
}

/// <summary>One reserved mapped name under one mapped parent directory.</summary>
public sealed record PathCollisionEntry(string SourceItemId, MappedItemKind Kind);

/// <summary>
/// Records which mapped names are taken under each mapped parent directory so the
/// deterministic mapper can detect collisions (ordinal case-insensitive, including
/// file-versus-folder conflicts) and so resume/rerun can reuse earlier mappings.
/// M4 provides the in-memory implementation; milestone M5 substitutes a
/// SQLite-backed implementation against this same seam so every mapping is persisted
/// and reused (contract rule 10).
/// </summary>
public interface IPathCollisionRegistry
{
    /// <summary>Finds the entry occupying <paramref name="mappedName" /> under a parent.</summary>
    PathCollisionEntry? Find(string parentKey, string mappedName);

    /// <summary>Finds the mapped name previously assigned to a source item under a parent.</summary>
    string? FindMappedNameByItemId(string parentKey, string sourceItemId);

    /// <summary>Reserves a mapped name for a source item under a parent.</summary>
    void Register(string parentKey, string mappedName, PathCollisionEntry entry);
}

/// <summary>In-memory collision registry used by unit tests and as the M4 default.</summary>
public sealed class InMemoryPathCollisionRegistry : IPathCollisionRegistry
{
    private readonly Dictionary<string, Dictionary<string, PathCollisionEntry>> _entries =
        new(StringComparer.Ordinal);

    public PathCollisionEntry? Find(string parentKey, string mappedName)
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentException.ThrowIfNullOrEmpty(mappedName);

        return _entries.TryGetValue(parentKey, out var children) &&
               children.TryGetValue(mappedName, out var entry)
            ? entry
            : null;
    }

    public string? FindMappedNameByItemId(string parentKey, string sourceItemId)
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentException.ThrowIfNullOrEmpty(sourceItemId);

        if (!_entries.TryGetValue(parentKey, out var children))
        {
            return null;
        }

        foreach (var (name, entry) in children)
        {
            if (string.Equals(entry.SourceItemId, sourceItemId, StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }

    public void Register(string parentKey, string mappedName, PathCollisionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentException.ThrowIfNullOrEmpty(mappedName);
        ArgumentNullException.ThrowIfNull(entry);

        if (!_entries.TryGetValue(parentKey, out var children))
        {
            children = new Dictionary<string, PathCollisionEntry>(StringComparer.OrdinalIgnoreCase);
            _entries[parentKey] = children;
        }

        children[mappedName] = entry;
    }
}
