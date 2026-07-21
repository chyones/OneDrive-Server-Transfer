namespace OneDriveServerTransfer.State;

/// <summary>
/// Creates the SQLite schema foundation for the application-owned operational state
/// database. Version 1 covers schema metadata plus the M4 destination source-binding
/// tables; inventory, mapping, resume, and checkpoint tables are added by milestone M5.
/// </summary>
public interface ITransferStateSchemaInitializer
{
    Task InitializeAsync(string databasePath, CancellationToken cancellationToken);
}
