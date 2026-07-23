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

    [Fact]
    public async Task InitializationReleasesTheDatabaseFileHandleImmediately()
    {
        var initializer = new SqliteTransferStateSchemaInitializer();

        await initializer.InitializeAsync(_databasePath, CancellationToken.None);

        // No pool clearing and no GC: with pooling disabled the initializer must not
        // retain any OS handle. On Windows a pooled connection intermittently kept the
        // file locked here (the TemporaryUrlIsNeverPersistedInState flake).
        using var stream = new FileStream(
            _databasePath, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task RepeatedInitializationAndImmediateFileAccessIsStable()
    {
        var initializer = new SqliteTransferStateSchemaInitializer();

        for (var iteration = 0; iteration < 25; iteration++)
        {
            await initializer.InitializeAsync(_databasePath, CancellationToken.None);

            // Immediate byte read and delete without pool clearing: any retained
            // handle fails this on Windows.
            var bytes = await File.ReadAllBytesAsync(_databasePath);
            Assert.True(bytes.Length > 0);
            File.Delete(_databasePath);
        }
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
        // Microsoft.Data.Sqlite pools connections; clear the pool so the pooled
        // connections release their file handles before the temp file is deleted.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
