namespace OneDriveServerTransfer.State;

/// <summary>
/// Creates the SQLite schema foundation for the application-owned operational state
/// database. M1 creates only schema metadata; operational tables for inventory, mapping,
/// resume, and checkpoints are added by milestone M5.
/// </summary>
public interface ITransferStateSchemaInitializer
{
    Task InitializeAsync(string databasePath, CancellationToken cancellationToken);
}
