using System.IO;
using Microsoft.Data.Sqlite;

namespace OneDriveServerTransfer.State;

/// <summary>
/// Initializes the version-1 state schema: a metadata table recording the supported
/// state-schema and path-mapping versions, the SQLite user_version pragma, the M4
/// destination source-binding tables, and the M5 transfer-item, run, scan, delta
/// checkpoint, and path-mapping tables. The initializer is idempotent and never drops
/// or rebuilds existing data.
/// </summary>
public sealed class SqliteTransferStateSchemaInitializer : ITransferStateSchemaInitializer
{
    public const int StateSchemaVersion = 1;
    public const int PathMappingVersion = 1;

    public async Task InitializeAsync(string databasePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS schema_metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, $"""
            INSERT OR IGNORE INTO schema_metadata (key, value) VALUES
                ('StateSchemaVersion', '{StateSchemaVersion}'),
                ('PathMappingVersion', '{PathMappingVersion}');
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS destination_binding (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                tenant_id TEXT NOT NULL,
                drive_id TEXT NOT NULL,
                employee_object_id TEXT NOT NULL,
                employee_upn TEXT NULL,
                bound_by_operator_object_id TEXT NOT NULL,
                bound_by_operator_upn TEXT NULL,
                bound_utc TEXT NOT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS destination_operator_audit (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                operator_object_id TEXT NOT NULL,
                operator_upn TEXT NULL,
                action TEXT NOT NULL,
                recorded_utc TEXT NOT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

        // M5: scan inventory, resume, crash recovery, run record, delta checkpoint,
        // and persisted path mappings (contract section 10 minimum, rule 10 of
        // section 11). All value states and classifications are stored as the exact
        // approved enum names.
        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS transfer_item (
                drive_id TEXT NOT NULL,
                source_item_id TEXT NOT NULL,
                parent_item_id TEXT NULL,
                item_name TEXT NOT NULL DEFAULT '',
                source_path TEXT NULL,
                mapped_relative_path TEXT NULL,
                facet_classification TEXT NOT NULL,
                etag TEXT NULL,
                ctag TEXT NULL,
                size_bytes INTEGER NULL,
                created_utc TEXT NULL,
                last_modified_utc TEXT NULL,
                source_hash_algorithm TEXT NULL,
                source_hash_value TEXT NULL,
                local_sha256 TEXT NULL,
                transfer_state TEXT NOT NULL,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                timestamp_preservation TEXT NOT NULL DEFAULT 'NotAttempted',
                scan_id TEXT NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY (drive_id, source_item_id)
            );
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE INDEX IF NOT EXISTS ix_transfer_item_state
                ON transfer_item (drive_id, transfer_state);
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE INDEX IF NOT EXISTS ix_transfer_item_parent
                ON transfer_item (drive_id, parent_item_id);
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS transfer_run (
                run_id TEXT PRIMARY KEY,
                drive_id TEXT NOT NULL,
                scan_id TEXT NULL,
                started_utc TEXT NOT NULL,
                ended_utc TEXT NULL,
                final_state TEXT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS delta_checkpoint (
                drive_id TEXT PRIMARY KEY,
                checkpoint TEXT NOT NULL,
                delta_state TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS scan_state (
                scan_id TEXT PRIMARY KEY,
                tenant_id TEXT NOT NULL,
                employee_object_id TEXT NOT NULL,
                drive_id TEXT NOT NULL,
                destination_root TEXT NOT NULL,
                state TEXT NOT NULL,
                started_utc TEXT NOT NULL,
                completed_utc TEXT NULL,
                file_count INTEGER NOT NULL DEFAULT 0,
                folder_count INTEGER NOT NULL DEFAULT 0,
                empty_folder_count INTEGER NOT NULL DEFAULT 0,
                unsupported_count INTEGER NOT NULL DEFAULT 0,
                known_bytes INTEGER NOT NULL DEFAULT 0
            );
            """, cancellationToken).ConfigureAwait(false);

        // mapped_name_key is the application-computed ordinal case-insensitive lookup
        // key (uppercase invariant form); mapped_name keeps the display form.
        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS path_mapping (
                parent_key TEXT NOT NULL,
                mapped_name TEXT NOT NULL,
                mapped_name_key TEXT NOT NULL,
                source_item_id TEXT NOT NULL,
                item_kind TEXT NOT NULL,
                PRIMARY KEY (parent_key, mapped_name_key)
            );
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction, """
            CREATE INDEX IF NOT EXISTS ix_path_mapping_item
                ON path_mapping (parent_key, source_item_id);
            """, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, transaction,
            $"PRAGMA user_version = {StateSchemaVersion};", cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
