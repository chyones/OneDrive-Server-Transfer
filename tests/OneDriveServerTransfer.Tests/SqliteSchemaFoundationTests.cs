using Microsoft.Data.Sqlite;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Verifies the M1 SQLite schema foundation: metadata creation, version records, and
/// idempotent re-initialization without data loss.
/// </summary>
public class SqliteSchemaFoundationTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"odst-m1-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task InitializeCreatesSchemaMetadataAndUserVersion()
    {
        var initializer = new SqliteTransferStateSchemaInitializer();

        await initializer.InitializeAsync(_databasePath, CancellationToken.None);

        Assert.True(File.Exists(_databasePath));

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        Assert.Equal(1L, await ReadUserVersionAsync(connection));
        Assert.Equal("1", await ReadMetadataValueAsync(connection, "StateSchemaVersion"));
        Assert.Equal("1", await ReadMetadataValueAsync(connection, "PathMappingVersion"));
    }

    [Fact]
    public async Task InitializeIsIdempotent()
    {
        var initializer = new SqliteTransferStateSchemaInitializer();

        await initializer.InitializeAsync(_databasePath, CancellationToken.None);
        await initializer.InitializeAsync(_databasePath, CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        Assert.Equal(1L, await ReadUserVersionAsync(connection));
        Assert.Equal("1", await ReadMetadataValueAsync(connection, "StateSchemaVersion"));
    }

    private static async Task<long> ReadUserVersionAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string?> ReadMetadataValueAsync(SqliteConnection connection, string key)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync();
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
