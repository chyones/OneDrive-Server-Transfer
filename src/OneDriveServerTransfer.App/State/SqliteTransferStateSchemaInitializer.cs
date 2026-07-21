using System.IO;
using Microsoft.Data.Sqlite;

namespace OneDriveServerTransfer.State;

/// <summary>
/// Initializes the version-1 state schema: a metadata table recording the supported
/// state-schema and path-mapping versions, the SQLite user_version pragma, and the M4
/// destination source-binding tables. The initializer is idempotent and never drops or
/// rebuilds existing data.
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
