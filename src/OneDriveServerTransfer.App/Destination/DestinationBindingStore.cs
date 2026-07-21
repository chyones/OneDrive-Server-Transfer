using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Destination;

/// <summary>A persisted destination source-binding row.</summary>
public sealed record StoredDestinationBinding(
    string TenantId,
    string DriveId,
    string EmployeeObjectId,
    string? EmployeeUpn,
    string BoundByOperatorObjectId,
    string? BoundByOperatorUpn,
    DateTimeOffset BoundUtc);

/// <summary>
/// SQLite persistence for the destination source binding inside the application-owned
/// state database at <c>_TransferReport\TransferState.db</c> (D-016). All writes are
/// transactional. The store never silently resets or rebuilds state: integrity or
/// version failures raise a reference-coded <see cref="DestinationException" />.
/// </summary>
public interface IDestinationBindingStore
{
    Task ValidateIntegrityAsync(string databasePath, CancellationToken cancellationToken);

    Task<StoredDestinationBinding?> GetBindingAsync(string databasePath, CancellationToken cancellationToken);

    Task CreateBindingAsync(
        string databasePath,
        StoredDestinationBinding binding,
        CancellationToken cancellationToken);

    Task RecordOperatorAuditAsync(
        string databasePath,
        OperatorIdentity operatorIdentity,
        string action,
        CancellationToken cancellationToken);
}

public sealed class SqliteDestinationBindingStore : IDestinationBindingStore
{
    private readonly ITransferStateSchemaInitializer _schemaInitializer;
    private readonly ILogger<SqliteDestinationBindingStore> _logger;

    public SqliteDestinationBindingStore(
        ITransferStateSchemaInitializer schemaInitializer,
        ILogger<SqliteDestinationBindingStore> logger)
    {
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ValidateIntegrityAsync(string databasePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(databasePath, cancellationToken).ConfigureAwait(false);

            await using var integrityCommand = connection.CreateCommand();
            integrityCommand.CommandText = "PRAGMA integrity_check;";
            var integrityResult = (string?)await integrityCommand
                .ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Destination state integrity check failed; code={ReferenceCode}",
                    DestinationErrorCodes.InvalidStateDatabase);
                throw DestinationErrors.InvalidStateDatabase();
            }

            var schemaVersion = await ReadMetadataValueAsync(connection, "StateSchemaVersion", cancellationToken)
                .ConfigureAwait(false);
            var mappingVersion = await ReadMetadataValueAsync(connection, "PathMappingVersion", cancellationToken)
                .ConfigureAwait(false);

            if (schemaVersion != SqliteTransferStateSchemaInitializer.StateSchemaVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ||
                mappingVersion != SqliteTransferStateSchemaInitializer.PathMappingVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
            {
                _logger.LogWarning(
                    "Destination state has an unsupported version; code={ReferenceCode}",
                    DestinationErrorCodes.InvalidStateDatabase);
                throw DestinationErrors.InvalidStateDatabase();
            }
        }
        catch (SqliteException exception)
        {
            _logger.LogWarning(
                "Destination state database could not be read; code={ReferenceCode}; sqliteError={SqliteErrorCode}",
                DestinationErrorCodes.InvalidStateDatabase, exception.SqliteErrorCode);
            throw DestinationErrors.InvalidStateDatabase(exception);
        }
    }

    public async Task<StoredDestinationBinding?> GetBindingAsync(
        string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenInitializedAsync(databasePath, cancellationToken)
            .ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tenant_id, drive_id, employee_object_id, employee_upn,
                   bound_by_operator_object_id, bound_by_operator_upn, bound_utc
            FROM destination_binding
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new StoredDestinationBinding(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            DateTimeOffset.Parse(reader.GetString(6), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind));
    }

    public async Task CreateBindingAsync(
        string databasePath,
        StoredDestinationBinding binding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);

        await using var connection = await OpenInitializedAsync(databasePath, cancellationToken)
            .ConfigureAwait(false);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO destination_binding
                (id, tenant_id, drive_id, employee_object_id, employee_upn,
                 bound_by_operator_object_id, bound_by_operator_upn, bound_utc)
            VALUES (1, $tenantId, $driveId, $employeeObjectId, $employeeUpn,
                    $operatorObjectId, $operatorUpn, $boundUtc);
            """;
        command.Parameters.AddWithValue("$tenantId", binding.TenantId);
        command.Parameters.AddWithValue("$driveId", binding.DriveId);
        command.Parameters.AddWithValue("$employeeObjectId", binding.EmployeeObjectId);
        command.Parameters.AddWithValue("$employeeUpn", (object?)binding.EmployeeUpn ?? DBNull.Value);
        command.Parameters.AddWithValue("$operatorObjectId", binding.BoundByOperatorObjectId);
        command.Parameters.AddWithValue("$operatorUpn", (object?)binding.BoundByOperatorUpn ?? DBNull.Value);
        command.Parameters.AddWithValue("$boundUtc", binding.BoundUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordOperatorAuditAsync(
        string databasePath,
        OperatorIdentity operatorIdentity,
        string action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operatorIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        await using var connection = await OpenInitializedAsync(databasePath, cancellationToken)
            .ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO destination_operator_audit
                (operator_object_id, operator_upn, action, recorded_utc)
            VALUES ($operatorObjectId, $operatorUpn, $action, $recordedUtc);
            """;
        command.Parameters.AddWithValue("$operatorObjectId", operatorIdentity.ObjectId);
        command.Parameters.AddWithValue("$operatorUpn", (object?)operatorIdentity.UserPrincipalName ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$recordedUtc", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenInitializedAsync(
        string databasePath, CancellationToken cancellationToken)
    {
        await _schemaInitializer.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        return await OpenAsync(databasePath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SqliteConnection> OpenAsync(string databasePath, CancellationToken cancellationToken)
    {
        // Pooling is disabled so a disposed connection releases its OS file handle
        // immediately: a database rejected by integrity validation must stay readable,
        // preservable, and copyable by the caller without a stale pooled lock.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<string?> ReadMetadataValueAsync(
        SqliteConnection connection, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }
}
