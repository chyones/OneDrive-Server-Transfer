using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.State;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Destination source binding: a destination binds once to tenant + drive + employee
/// object IDs, resume validates the same source for any authorized operator (operator
/// identity is audit data, never binding; D-032), foreign sources are rejected, and a
/// non-empty destination without valid application state is never silently adopted.
/// </summary>
public class DestinationBindingServiceTests : IDisposable
{
    private static readonly SourceBindingIdentity Source = new(
        "11111111-1111-1111-1111-111111111111",
        "drive-abc-123",
        "22222222-2222-2222-2222-222222222222",
        "employee@example.com");

    private static readonly OperatorIdentity FirstOperator =
        new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "operator1@example.com");

    private static readonly OperatorIdentity SecondOperator =
        new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "operator2@example.com");

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

    private readonly ResolvedDestination _destination;

    public DestinationBindingServiceTests()
    {
        var layout = new DestinationLayoutService(NullLogger<DestinationLayoutService>.Instance);
        _destination = layout.EnsureLayout(_root);
    }

    private static DestinationBindingService CreateService() =>
        new(
            new SqliteDestinationBindingStore(
                new SqliteTransferStateSchemaInitializer(),
                NullLogger<SqliteDestinationBindingStore>.Instance),
            new DestinationLayoutService(NullLogger<DestinationLayoutService>.Instance),
            NullLogger<DestinationBindingService>.Instance);

    [Fact]
    public async Task EmptyDestinationIsBoundToTheSource()
    {
        var service = CreateService();

        var result = await service.BindOrValidateAsync(
            _destination, Source, FirstOperator, CancellationToken.None);

        Assert.Equal(DestinationBindingOutcome.BoundNew, result.Outcome);
        Assert.True(File.Exists(_destination.StateDatabasePath));

        await using var connection = await OpenDatabaseAsync();
        Assert.Equal(Source.TenantId, await ReadBindingColumnAsync(connection, "tenant_id"));
        Assert.Equal(Source.DriveId, await ReadBindingColumnAsync(connection, "drive_id"));
        Assert.Equal(Source.EmployeeObjectId, await ReadBindingColumnAsync(connection, "employee_object_id"));
        Assert.Equal(Source.EmployeeUpn, await ReadBindingColumnAsync(connection, "employee_upn"));
        Assert.Equal(FirstOperator.ObjectId, await ReadBindingColumnAsync(connection, "bound_by_operator_object_id"));
        Assert.Equal(1L, await CountAuditRowsAsync(connection, "Bound"));
    }

    [Fact]
    public async Task SameSourceResumeSucceedsForAnotherAuthorizedOperator()
    {
        var service = CreateService();
        await service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None);

        var result = await service.BindOrValidateAsync(
            _destination, Source, SecondOperator, CancellationToken.None);

        Assert.Equal(DestinationBindingOutcome.ResumedExisting, result.Outcome);

        await using var connection = await OpenDatabaseAsync();
        Assert.Equal(1L, await CountAuditRowsAsync(connection, "ResumeValidated"));
        // The binding still records only the original source; the second operator is audit data.
        Assert.Equal(FirstOperator.ObjectId, await ReadBindingColumnAsync(connection, "bound_by_operator_object_id"));
    }

    [Fact]
    public async Task SameSourceResumeSucceedsForTheSameOperator()
    {
        var service = CreateService();
        await service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None);

        var result = await service.BindOrValidateAsync(
            _destination, Source, FirstOperator, CancellationToken.None);

        Assert.Equal(DestinationBindingOutcome.ResumedExisting, result.Outcome);
    }

    [Fact]
    public async Task DifferentDriveIsRejectedAsForeignSource()
    {
        var service = CreateService();
        await service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None);

        var foreign = Source with { DriveId = "drive-other-999" };
        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.BindOrValidateAsync(_destination, foreign, SecondOperator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.ForeignSourceBinding, exception.ReferenceCode);
    }

    [Fact]
    public async Task DifferentTenantIsRejectedAsForeignSource()
    {
        var service = CreateService();
        await service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None);

        var foreign = Source with { TenantId = "99999999-9999-9999-9999-999999999999" };
        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.BindOrValidateAsync(_destination, foreign, SecondOperator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.ForeignSourceBinding, exception.ReferenceCode);
    }

    [Fact]
    public async Task DifferentEmployeeIsRejectedAsForeignSource()
    {
        var service = CreateService();
        await service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None);

        var foreign = Source with { EmployeeObjectId = "33333333-3333-3333-3333-333333333333" };
        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.BindOrValidateAsync(_destination, foreign, SecondOperator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.ForeignSourceBinding, exception.ReferenceCode);
    }

    [Fact]
    public async Task NonEmptyDestinationWithoutStateIsRejected()
    {
        File.WriteAllText(Path.Combine(_destination.ContentRootPath, "stray.txt"), "data");
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.NonEmptyDestinationWithoutState, exception.ReferenceCode);
        Assert.False(File.Exists(_destination.StateDatabasePath));
    }

    [Fact]
    public async Task ForeignFilesInStateDirectoryWithoutStateAreRejected()
    {
        File.WriteAllText(Path.Combine(_destination.StateRootPath, "notes.txt"), "data");
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.NonEmptyDestinationWithoutState, exception.ReferenceCode);
    }

    [Fact]
    public async Task UnsupportedFutureSchemaVersionIsRejected()
    {
        var service = CreateService();
        await service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None);

        await using (var connection = await OpenDatabaseAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_metadata SET value = '2' WHERE key = 'StateSchemaVersion';";
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.BindOrValidateAsync(_destination, Source, SecondOperator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.InvalidStateDatabase, exception.ReferenceCode);
    }

    [Fact]
    public async Task CorruptStateDatabaseIsRejectedAndNeverReset()
    {
        await File.WriteAllBytesAsync(
            _destination.StateDatabasePath, "not a sqlite database"u8.ToArray());
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.BindOrValidateAsync(_destination, Source, FirstOperator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.InvalidStateDatabase, exception.ReferenceCode);
        // The original bytes are preserved; the store must never silently reset state.
        Assert.Equal("not a sqlite database", await File.ReadAllTextAsync(_destination.StateDatabasePath));
    }

    private async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection = new SqliteConnection($"Data Source={_destination.StateDatabasePath}");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<string?> ReadBindingColumnAsync(SqliteConnection connection, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {column} FROM destination_binding WHERE id = 1;";
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task<long> CountAuditRowsAsync(SqliteConnection connection, string action)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM destination_operator_audit WHERE action = $action;";
        command.Parameters.AddWithValue("$action", action);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools connections; clear the pool so the pooled
        // connections release their file handles before the temp tree is deleted.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
