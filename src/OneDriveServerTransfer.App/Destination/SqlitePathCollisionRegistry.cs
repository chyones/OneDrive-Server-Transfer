using Microsoft.Data.Sqlite;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// SQLite-backed <see cref="IPathCollisionRegistry" /> persisting every source-to-local
/// mapping in the application-owned state database so mappings are reused on resume and
/// rerun (contract section 11, rule 10). <see cref="PathMapperV1" /> works unchanged
/// against this seam. Ordinal case-insensitive matching is implemented with an
/// application-computed uppercase-invariant lookup key, self-consistent between writes
/// and lookups. Pooling is disabled, matching the other state stores. The registry is
/// bound to one destination database by <see cref="Open" /> before use; the single
/// window runs one operation at a time, so the registered singleton is never shared by
/// concurrent destinations.
/// </summary>
public sealed class SqlitePathCollisionRegistry : IPathCollisionRegistry
{
    private readonly ITransferStateSchemaInitializer _schemaInitializer;
    private string? _databasePath;

    public SqlitePathCollisionRegistry(ITransferStateSchemaInitializer schemaInitializer)
    {
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    /// <summary>
    /// Binds the registry to a destination state database, initializing the schema
    /// idempotently. Reopening the same or another database is safe; previously
    /// persisted mappings are reused.
    /// </summary>
    public void Open(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _schemaInitializer.InitializeAsync(databasePath, CancellationToken.None)
            .GetAwaiter().GetResult();
        _databasePath = databasePath;
    }

    public PathCollisionEntry? Find(string parentKey, string mappedName)
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentException.ThrowIfNullOrEmpty(mappedName);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_item_id, item_kind
            FROM path_mapping
            WHERE parent_key = $parentKey AND mapped_name_key = $mappedNameKey;
            """;
        command.Parameters.AddWithValue("$parentKey", parentKey);
        command.Parameters.AddWithValue("$mappedNameKey", LookupKey(mappedName));

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PathCollisionEntry(
            reader.GetString(0),
            Enum.Parse<MappedItemKind>(reader.GetString(1)));
    }

    public string? FindMappedNameByItemId(string parentKey, string sourceItemId)
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentException.ThrowIfNullOrEmpty(sourceItemId);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT mapped_name
            FROM path_mapping
            WHERE parent_key = $parentKey AND source_item_id = $sourceItemId
            ORDER BY rowid DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$parentKey", parentKey);
        command.Parameters.AddWithValue("$sourceItemId", sourceItemId);

        return (string?)command.ExecuteScalar();
    }

    public void Register(string parentKey, string mappedName, PathCollisionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentException.ThrowIfNullOrEmpty(mappedName);
        ArgumentNullException.ThrowIfNull(entry);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO path_mapping
                (parent_key, mapped_name, mapped_name_key, source_item_id, item_kind)
            VALUES
                ($parentKey, $mappedName, $mappedNameKey, $sourceItemId, $itemKind);
            """;
        command.Parameters.AddWithValue("$parentKey", parentKey);
        command.Parameters.AddWithValue("$mappedName", mappedName);
        command.Parameters.AddWithValue("$mappedNameKey", LookupKey(mappedName));
        command.Parameters.AddWithValue("$sourceItemId", entry.SourceItemId);
        command.Parameters.AddWithValue("$itemKind", entry.Kind.ToString());
        command.ExecuteNonQuery();
    }

    private static string LookupKey(string mappedName) => mappedName.ToUpperInvariant();

    private SqliteConnection OpenConnection()
    {
        if (_databasePath is null)
        {
            throw new InvalidOperationException(
                "The collision registry has not been opened against a destination state database.");
        }

        // Pooling is disabled so a disposed connection releases its OS file handle
        // immediately, matching the other SQLite state stores.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false,
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
