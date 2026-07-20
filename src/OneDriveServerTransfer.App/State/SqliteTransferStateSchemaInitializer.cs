using System.IO;
using Microsoft.Data.Sqlite;

namespace OneDriveServerTransfer.State;

/// <summary>
/// Initializes the empty M1 schema foundation: a metadata table recording the supported
/// state-schema and path-mapping versions, plus the SQLite user_version pragma. The
/// initializer is idempotent and never drops or rebuilds existing data.
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
