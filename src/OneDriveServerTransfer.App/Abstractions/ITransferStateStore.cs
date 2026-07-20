namespace OneDriveServerTransfer.Abstractions;

/// <summary>
/// Application-owned SQLite operational state (decision D-016). Implemented in milestone
/// M5. SQLite is the operational authority for source binding, scan inventory, resume,
/// crash recovery, path mappings, and the delta checkpoint; CSV and JSON reports are
/// audit output only and are never used as the operational resume database.
/// </summary>
public interface ITransferStateStore
{
    Task OpenAsync(string databasePath, CancellationToken cancellationToken);

    Task<string?> GetDeltaCheckpointAsync(CancellationToken cancellationToken);

    Task SaveDeltaCheckpointAsync(string deltaCheckpoint, CancellationToken cancellationToken);
}
