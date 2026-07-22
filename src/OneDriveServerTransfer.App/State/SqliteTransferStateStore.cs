using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;

namespace OneDriveServerTransfer.State;

/// <summary>
/// SQLite implementation of <see cref="ITransferStateStore" /> against the
/// application-owned database at <c>_TransferReport\TransferState.db</c> (D-016).
/// Every write is transactional and durable; upserts by Drive Item ID make page replay
/// idempotent, and a page and its paging-link checkpoint always commit in one
/// transaction so a crash can never skip applied data or advance past unapplied data.
/// Pooling is disabled so a disposed connection releases its OS file handle
/// immediately, matching the M4 binding store. The store never silently resets or
/// rebuilds state: integrity or version failures raise a reference-coded
/// <see cref="DestinationException" />.
/// </summary>
public sealed class SqliteTransferStateStore : ITransferStateStore
{
    // Upsert refreshes scan-visible metadata on every occurrence (last occurrence
    // wins). Transfer progress made by a later milestone (Downloading and beyond) is
    // preserved; only pre-transfer scan states are reset so a re-scan re-maps cleanly.
    private const string UpsertItemSql = """
        INSERT INTO transfer_item
            (drive_id, source_item_id, parent_item_id, item_name, source_path,
             mapped_relative_path, facet_classification, etag, ctag, size_bytes,
             created_utc, last_modified_utc, source_hash_algorithm, source_hash_value,
             local_sha256, transfer_state, attempt_count, timestamp_preservation,
             scan_id, updated_utc)
        VALUES
            ($driveId, $sourceItemId, $parentItemId, $itemName, NULL,
             NULL, $classification, $etag, $ctag, $sizeBytes,
             $createdUtc, $lastModifiedUtc, $sourceHashAlgorithm, $sourceHashValue,
             NULL, $transferState, 0, 'NotAttempted',
             $scanId, $updatedUtc)
        ON CONFLICT (drive_id, source_item_id) DO UPDATE SET
            parent_item_id = excluded.parent_item_id,
            item_name = excluded.item_name,
            facet_classification = excluded.facet_classification,
            etag = excluded.etag,
            ctag = excluded.ctag,
            size_bytes = excluded.size_bytes,
            created_utc = excluded.created_utc,
            last_modified_utc = excluded.last_modified_utc,
            source_hash_algorithm = excluded.source_hash_algorithm,
            source_hash_value = excluded.source_hash_value,
            scan_id = excluded.scan_id,
            updated_utc = excluded.updated_utc,
            source_path = CASE
                WHEN transfer_item.transfer_state IN ('Discovered', 'Mapped', 'Unsupported') THEN NULL
                ELSE transfer_item.source_path END,
            mapped_relative_path = CASE
                WHEN transfer_item.transfer_state IN ('Discovered', 'Mapped', 'Unsupported') THEN NULL
                ELSE transfer_item.mapped_relative_path END,
            transfer_state = CASE
                WHEN transfer_item.transfer_state IN ('Discovered', 'Mapped', 'Unsupported') THEN excluded.transfer_state
                ELSE transfer_item.transfer_state END;
        """;

    private const string ItemColumns = """
            drive_id, source_item_id, parent_item_id, item_name, source_path,
            mapped_relative_path, facet_classification, etag, ctag, size_bytes,
            created_utc, last_modified_utc, source_hash_algorithm, source_hash_value,
            local_sha256, transfer_state, attempt_count, timestamp_preservation,
            scan_id, updated_utc
        """;

    private const string ScanColumns = """
            scan_id, tenant_id, employee_object_id, drive_id, destination_root,
            state, started_utc, completed_utc, file_count, folder_count,
            empty_folder_count, unsupported_count, known_bytes
        """;

    private static readonly string SelectItemSql = $"SELECT {ItemColumns} FROM transfer_item";

    private readonly ITransferStateSchemaInitializer _schemaInitializer;
    private readonly ILogger<SqliteTransferStateStore> _logger;

    private string? _databasePath;
    private string? _driveId;

    public SqliteTransferStateStore(
        ITransferStateSchemaInitializer schemaInitializer,
        ILogger<SqliteTransferStateStore> logger)
    {
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string DriveId => _driveId ?? throw new InvalidOperationException(
        "The transfer state store has not been opened.");

    public async Task OpenAsync(string databasePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        await _schemaInitializer.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        await ValidateIntegrityAsync(databasePath, cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(databasePath, cancellationToken)
            .ConfigureAwait(false);
        var driveId = await ReadBindingDriveIdAsync(connection, cancellationToken).ConfigureAwait(false);
        if (driveId is null)
        {
            _logger.LogWarning(
                "Transfer state has no source binding; code={ReferenceCode}",
                DestinationErrorCodes.InvalidStateDatabase);
            throw DestinationErrors.InvalidStateDatabase();
        }

        _databasePath = databasePath;
        _driveId = driveId;
    }

    public async Task<string?> GetDeltaCheckpointAsync(CancellationToken cancellationToken)
    {
        var record = await GetDeltaCheckpointRecordAsync(cancellationToken).ConfigureAwait(false);
        return record?.Checkpoint;
    }

    public Task SaveDeltaCheckpointAsync(string deltaCheckpoint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaCheckpoint);
        return SaveDeltaCheckpointRecordAsync(
            new DeltaCheckpointRecord(DriveId, deltaCheckpoint,
                DeltaCheckpointState.DeltaCheckpointValid, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<DeltaCheckpointRecord?> GetDeltaCheckpointRecordAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT checkpoint, delta_state, updated_utc
            FROM delta_checkpoint
            WHERE drive_id = $driveId;
            """;
        command.Parameters.AddWithValue("$driveId", DriveId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new DeltaCheckpointRecord(
            DriveId,
            reader.GetString(0),
            ParseEnum<DeltaCheckpointState>(reader.GetString(1)),
            ParseUtc(reader.GetString(2)));
    }

    public async Task SaveDeltaCheckpointRecordAsync(
        DeltaCheckpointRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertCheckpointAsync(connection, transaction, record, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task BeginScanAsync(ScanRecord scan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scan);

        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO scan_state
                (scan_id, tenant_id, employee_object_id, drive_id, destination_root,
                 state, started_utc)
            VALUES
                ($scanId, $tenantId, $employeeObjectId, $driveId, $destinationRoot,
                 $state, $startedUtc);
            """;
        command.Parameters.AddWithValue("$scanId", scan.ScanId);
        command.Parameters.AddWithValue("$tenantId", scan.TenantId);
        command.Parameters.AddWithValue("$employeeObjectId", scan.EmployeeObjectId);
        command.Parameters.AddWithValue("$driveId", scan.DriveId);
        command.Parameters.AddWithValue("$destinationRoot", scan.DestinationRoot);
        command.Parameters.AddWithValue("$state", ScanState.InProgress.ToString());
        command.Parameters.AddWithValue("$startedUtc", FormatUtc(scan.StartedUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> MarkInProgressScansInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE scan_state
            SET state = $interrupted
            WHERE state = $inProgress;
            """;
        command.Parameters.AddWithValue("$interrupted", ScanState.Interrupted.ToString());
        command.Parameters.AddWithValue("$inProgress", ScanState.InProgress.ToString());
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task ApplyDeltaPageAsync(
        string scanId,
        IReadOnlyList<TransferItemRecord> items,
        string nextLink,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nextLink);
        return ApplyPageAsync(scanId, items, nextLink,
            DeltaCheckpointState.InitialEnumerationInProgress, cancellationToken);
    }

    public Task ApplyFinalDeltaPageAsync(
        string scanId,
        IReadOnlyList<TransferItemRecord> items,
        string deltaLink,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaLink);
        return ApplyPageAsync(scanId, items, deltaLink,
            DeltaCheckpointState.InitialEnumerationComplete, cancellationToken);
    }

    public async Task<ScanRecord> CompleteScanAsync(string scanId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanId);

        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Folders with no active children are empty folders. Tombstones and unsupported
        // content never make a parent non-empty.
        await ExecuteAsync(connection, transaction, """
            UPDATE transfer_item
            SET facet_classification = $emptyFolder
            WHERE drive_id = $driveId
              AND facet_classification = $folder
              AND source_item_id NOT IN (
                  SELECT DISTINCT parent_item_id FROM transfer_item
                  WHERE drive_id = $driveId
                    AND parent_item_id IS NOT NULL
                    AND facet_classification NOT IN ($deleted));
            """,
            cancellationToken,
            ("$emptyFolder", ItemFacetClassification.EmptyFolder.ToString()),
            ("$folder", ItemFacetClassification.Folder.ToString()),
            ("$deleted", ItemFacetClassification.DeletedSource.ToString()),
            ("$driveId", DriveId)).ConfigureAwait(false);

        // The drive root row is the archive container, not copied content: it is
        // excluded from every count.
        await ExecuteAsync(connection, transaction, """
            UPDATE scan_state
            SET state = $succeeded,
                completed_utc = $completedUtc,
                file_count = (SELECT COUNT(*) FROM transfer_item
                    WHERE drive_id = $driveId AND facet_classification = $file),
                folder_count = (SELECT COUNT(*) FROM transfer_item
                    WHERE drive_id = $driveId AND parent_item_id IS NOT NULL
                      AND facet_classification IN ($folder, $emptyFolder)),
                empty_folder_count = (SELECT COUNT(*) FROM transfer_item
                    WHERE drive_id = $driveId AND parent_item_id IS NOT NULL
                      AND facet_classification = $emptyFolder),
                unsupported_count = (SELECT COUNT(*) FROM transfer_item
                    WHERE drive_id = $driveId
                      AND facet_classification IN ($package, $shortcut, $unknown)),
                known_bytes = (SELECT COALESCE(SUM(size_bytes), 0) FROM transfer_item
                    WHERE drive_id = $driveId AND facet_classification = $file)
            WHERE scan_id = $scanId;
            """,
            cancellationToken,
            ("$succeeded", ScanState.Succeeded.ToString()),
            ("$completedUtc", FormatUtc(DateTimeOffset.UtcNow)),
            ("$file", ItemFacetClassification.File.ToString()),
            ("$folder", ItemFacetClassification.Folder.ToString()),
            ("$emptyFolder", ItemFacetClassification.EmptyFolder.ToString()),
            ("$package", ItemFacetClassification.UnsupportedPackage.ToString()),
            ("$shortcut", ItemFacetClassification.ExternalShortcut.ToString()),
            ("$unknown", ItemFacetClassification.Unknown.ToString()),
            ("$driveId", DriveId),
            ("$scanId", scanId)).ConfigureAwait(false);

        var checkpoint = await ReadCheckpointAsync(connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is not null &&
            checkpoint.State == DeltaCheckpointState.InitialEnumerationComplete)
        {
            await UpsertCheckpointAsync(connection, transaction,
                checkpoint with
                {
                    State = DeltaCheckpointState.DeltaCheckpointValid,
                    UpdatedUtc = DateTimeOffset.UtcNow
                },
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var finalized = await GetScanAsync(scanId, cancellationToken).ConfigureAwait(false);
        if (finalized is null || finalized.State != ScanState.Succeeded)
        {
            throw DestinationErrors.DestinationStateFailure();
        }

        _logger.LogInformation(
            "Scan completed; fileCount={FileCount}; folderCount={FolderCount}; unsupportedCount={UnsupportedCount}",
            finalized.FileCount, finalized.FolderCount, finalized.UnsupportedCount);
        return finalized;
    }

    public async Task<ScanRecord?> GetLatestSuccessfulScanAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {ScanColumns}
            FROM scan_state
            WHERE drive_id = $driveId AND state = $succeeded
            ORDER BY completed_utc DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$driveId", DriveId);
        command.Parameters.AddWithValue("$succeeded", ScanState.Succeeded.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadScan(reader)
            : null;
    }

    public async Task<TransferItemRecord?> GetItemAsync(
        string sourceItemId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceItemId);

        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectItemSql}
            WHERE drive_id = $driveId AND source_item_id = $sourceItemId;
            """;
        command.Parameters.AddWithValue("$driveId", DriveId);
        command.Parameters.AddWithValue("$sourceItemId", sourceItemId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadItem(reader)
            : null;
    }

    public Task<IReadOnlyList<TransferItemRecord>> GetItemsAwaitingSourcePathAsync(
        CancellationToken cancellationToken) =>
        QueryItemsAsync($"""
            {SelectItemSql} i
            WHERE i.drive_id = $driveId
              AND i.source_path IS NULL
              AND i.facet_classification <> $deleted
              AND (i.parent_item_id IS NULL
                   OR EXISTS (SELECT 1 FROM transfer_item p
                              WHERE p.drive_id = i.drive_id
                                AND p.source_item_id = i.parent_item_id
                                AND p.source_path IS NOT NULL))
            ORDER BY i.rowid;
            """,
            cancellationToken,
            ("$deleted", ItemFacetClassification.DeletedSource.ToString()));

    public Task<IReadOnlyList<TransferItemRecord>> GetUnresolvedItemsAsync(
        CancellationToken cancellationToken) =>
        QueryItemsAsync($"""
            {SelectItemSql}
            WHERE drive_id = $driveId
              AND source_path IS NULL
              AND facet_classification <> $deleted;
            """,
            cancellationToken,
            ("$deleted", ItemFacetClassification.DeletedSource.ToString()));

    public async Task UpdateItemPathsAsync(
        string sourceItemId,
        string sourcePath,
        string? mappedRelativePath,
        TransferItemState transferState,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceItemId);
        ArgumentNullException.ThrowIfNull(sourcePath);

        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE transfer_item
            SET source_path = $sourcePath,
                mapped_relative_path = $mappedRelativePath,
                transfer_state = $transferState,
                updated_utc = $updatedUtc
            WHERE drive_id = $driveId AND source_item_id = $sourceItemId;
            """;
        command.Parameters.AddWithValue("$sourcePath", sourcePath);
        command.Parameters.AddWithValue("$mappedRelativePath",
            (object?)mappedRelativePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$transferState", transferState.ToString());
        command.Parameters.AddWithValue("$updatedUtc", FormatUtc(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$driveId", DriveId);
        command.Parameters.AddWithValue("$sourceItemId", sourceItemId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TransferItemRecord>> GetItemsByStateAsync(
        TransferItemState state,
        CancellationToken cancellationToken) =>
        QueryItemsAsync($"""
            {SelectItemSql}
            WHERE drive_id = $driveId AND transfer_state = $state
            ORDER BY rowid;
            """,
            cancellationToken,
            ("$state", state.ToString()));

    public Task<IReadOnlyList<TransferItemRecord>> GetItemsByClassificationAsync(
        ItemFacetClassification classification,
        CancellationToken cancellationToken) =>
        QueryItemsAsync($"""
            {SelectItemSql}
            WHERE drive_id = $driveId AND facet_classification = $classification
            ORDER BY rowid;
            """,
            cancellationToken,
            ("$classification", classification.ToString()));

    public async Task SetItemStateAsync(
        string sourceItemId,
        TransferItemState state,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceItemId);

        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE transfer_item
            SET transfer_state = $state, updated_utc = $updatedUtc
            WHERE drive_id = $driveId AND source_item_id = $sourceItemId;
            """;
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$updatedUtc", FormatUtc(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$driveId", DriveId);
        command.Parameters.AddWithValue("$sourceItemId", sourceItemId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ResetInFlightItemsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE transfer_item
            SET transfer_state = $mapped, updated_utc = $updatedUtc
            WHERE drive_id = $driveId AND transfer_state = $downloading;
            """;
        command.Parameters.AddWithValue("$mapped", TransferItemState.Mapped.ToString());
        command.Parameters.AddWithValue("$downloading", TransferItemState.Downloading.ToString());
        command.Parameters.AddWithValue("$updatedUtc", FormatUtc(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$driveId", DriveId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyPageAsync(
        string scanId,
        IReadOnlyList<TransferItemRecord> items,
        string pagingLink,
        DeltaCheckpointState checkpointState,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanId);
        ArgumentNullException.ThrowIfNull(items);

        try
        {
            await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction =
                (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken)
                    .ConfigureAwait(false);

            foreach (var item in items)
            {
                await UpsertItemAsync(connection, transaction, scanId, item, cancellationToken)
                    .ConfigureAwait(false);
            }

            // The page and its opaque paging link commit together: a crash either
            // replays this page idempotently or resumes from this exact link.
            await UpsertCheckpointAsync(connection, transaction,
                new DeltaCheckpointRecord(DriveId, pagingLink, checkpointState, DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            _logger.LogWarning(
                "Delta page could not be applied; code={ReferenceCode}; sqliteError={SqliteErrorCode}",
                DestinationErrorCodes.DestinationStateFailure,
                (exception as SqliteException)?.SqliteErrorCode.ToString() ?? "n/a");
            throw DestinationErrors.DestinationStateFailure(exception);
        }
    }

    private async Task UpsertItemAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string scanId,
        TransferItemRecord item,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertItemSql;
        command.Parameters.AddWithValue("$driveId", DriveId);
        command.Parameters.AddWithValue("$sourceItemId", item.SourceItemId);
        command.Parameters.AddWithValue("$parentItemId", (object?)item.ParentItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$itemName", item.ItemName);
        command.Parameters.AddWithValue("$classification", item.Classification.ToString());
        command.Parameters.AddWithValue("$etag", (object?)item.ETag ?? DBNull.Value);
        command.Parameters.AddWithValue("$ctag", (object?)item.CTag ?? DBNull.Value);
        command.Parameters.AddWithValue("$sizeBytes", (object?)item.SizeBytes ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdUtc",
            item.CreatedUtc is { } created ? FormatUtc(created) : DBNull.Value);
        command.Parameters.AddWithValue("$lastModifiedUtc",
            item.LastModifiedUtc is { } modified ? FormatUtc(modified) : DBNull.Value);
        command.Parameters.AddWithValue("$sourceHashAlgorithm",
            (object?)item.SourceHashAlgorithm ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceHashValue",
            (object?)item.SourceHashValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$transferState", item.TransferState.ToString());
        command.Parameters.AddWithValue("$scanId", scanId);
        command.Parameters.AddWithValue("$updatedUtc", FormatUtc(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<TransferItemRecord>> QueryItemsAsync(
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("$driveId", DriveId);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var items = new List<TransferItemRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    private async Task<ScanRecord?> GetScanAsync(string scanId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenRequiredAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ScanColumns} FROM scan_state WHERE scan_id = $scanId;";
        command.Parameters.AddWithValue("$scanId", scanId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadScan(reader)
            : null;
    }

    private async Task<DeltaCheckpointRecord?> ReadCheckpointAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT checkpoint, delta_state, updated_utc
            FROM delta_checkpoint
            WHERE drive_id = $driveId;
            """;
        command.Parameters.AddWithValue("$driveId", DriveId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new DeltaCheckpointRecord(
            DriveId,
            reader.GetString(0),
            ParseEnum<DeltaCheckpointState>(reader.GetString(1)),
            ParseUtc(reader.GetString(2)));
    }

    private static async Task UpsertCheckpointAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DeltaCheckpointRecord record,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO delta_checkpoint (drive_id, checkpoint, delta_state, updated_utc)
            VALUES ($driveId, $checkpoint, $deltaState, $updatedUtc)
            ON CONFLICT (drive_id) DO UPDATE SET
                checkpoint = excluded.checkpoint,
                delta_state = excluded.delta_state,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$driveId", record.DriveId);
        command.Parameters.AddWithValue("$checkpoint", record.Checkpoint);
        command.Parameters.AddWithValue("$deltaState", record.State.ToString());
        command.Parameters.AddWithValue("$updatedUtc", FormatUtc(record.UpdatedUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ValidateIntegrityAsync(string databasePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(databasePath, cancellationToken)
                .ConfigureAwait(false);

            await using var integrityCommand = connection.CreateCommand();
            integrityCommand.CommandText = "PRAGMA integrity_check;";
            var integrityResult = (string?)await integrityCommand
                .ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Transfer state integrity check failed; code={ReferenceCode}",
                    DestinationErrorCodes.InvalidStateDatabase);
                throw DestinationErrors.InvalidStateDatabase();
            }

            var schemaVersion = await ReadMetadataValueAsync(
                connection, "StateSchemaVersion", cancellationToken).ConfigureAwait(false);
            var mappingVersion = await ReadMetadataValueAsync(
                connection, "PathMappingVersion", cancellationToken).ConfigureAwait(false);

            if (schemaVersion != SqliteTransferStateSchemaInitializer.StateSchemaVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ||
                mappingVersion != SqliteTransferStateSchemaInitializer.PathMappingVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
            {
                _logger.LogWarning(
                    "Transfer state has an unsupported version; code={ReferenceCode}",
                    DestinationErrorCodes.InvalidStateDatabase);
                throw DestinationErrors.InvalidStateDatabase();
            }
        }
        catch (SqliteException exception)
        {
            _logger.LogWarning(
                "Transfer state database could not be read; code={ReferenceCode}; sqliteError={SqliteErrorCode}",
                DestinationErrorCodes.InvalidStateDatabase, exception.SqliteErrorCode);
            throw DestinationErrors.InvalidStateDatabase(exception);
        }
    }

    private async Task<SqliteConnection> OpenRequiredAsync(CancellationToken cancellationToken)
    {
        if (_databasePath is null)
        {
            throw new InvalidOperationException("The transfer state store has not been opened.");
        }

        return await OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(
        string databasePath,
        CancellationToken cancellationToken)
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

    private static async Task<string?> ReadBindingDriveIdAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT drive_id FROM destination_binding WHERE id = 1;";
        return (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadMetadataValueAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private TransferItemRecord ReadItem(SqliteDataReader reader) => new(
        DriveId,
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        ParseEnum<ItemFacetClassification>(reader.GetString(6)),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetInt64(9),
        reader.IsDBNull(10) ? null : ParseUtc(reader.GetString(10)),
        reader.IsDBNull(11) ? null : ParseUtc(reader.GetString(11)),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13),
        reader.IsDBNull(14) ? null : reader.GetString(14),
        ParseEnum<TransferItemState>(reader.GetString(15)),
        reader.GetInt32(16),
        ParseEnum<TimestampPreservationResult>(reader.GetString(17)),
        reader.IsDBNull(18) ? null : reader.GetString(18),
        ParseUtc(reader.GetString(19)));

    private static ScanRecord ReadScan(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        ParseEnum<ScanState>(reader.GetString(5)),
        ParseUtc(reader.GetString(6)),
        reader.IsDBNull(7) ? null : ParseUtc(reader.GetString(7)),
        reader.GetInt64(8),
        reader.GetInt64(9),
        reader.GetInt64(10),
        reader.GetInt64(11),
        reader.GetInt64(12));

    private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            // Persisted state holds only approved values; anything else means the
            // database is not one this build can safely interpret.
            throw DestinationErrors.InvalidStateDatabase();
        }

        return parsed;
    }

    private static string FormatUtc(DateTimeOffset value) => value.ToString("O");

    private static DateTimeOffset ParseUtc(string value) =>
        DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
}
