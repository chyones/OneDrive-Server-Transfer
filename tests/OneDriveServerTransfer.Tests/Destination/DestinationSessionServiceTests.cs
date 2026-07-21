using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// End-to-end destination open: validation, layout, exclusive lock, and binding. A
/// second open fails with <c>DST-LOCK-001</c> while a session is held; reopening after
/// disposal validates the same-source binding as a resume.
/// </summary>
public class DestinationSessionServiceTests : IDisposable
{
    private static readonly SourceBindingIdentity Source = new(
        "11111111-1111-1111-1111-111111111111",
        "drive-abc-123",
        "22222222-2222-2222-2222-222222222222",
        "employee@example.com");

    private static readonly OperatorIdentity Operator =
        new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "operator@example.com");

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"odst-m4-{Guid.NewGuid():N}");

    private static DestinationSessionService CreateService() =>
        new(
            new DestinationValidator(
                new FakeFileSystemProbe(), [], NullLogger<DestinationValidator>.Instance),
            new DestinationLayoutService(NullLogger<DestinationLayoutService>.Instance),
            new DestinationLockService(NullLogger<DestinationLockService>.Instance),
            new DestinationBindingService(
                new SqliteDestinationBindingStore(
                    new SqliteTransferStateSchemaInitializer(),
                    NullLogger<SqliteDestinationBindingStore>.Instance),
                new DestinationLayoutService(NullLogger<DestinationLayoutService>.Instance),
                NullLogger<DestinationBindingService>.Instance),
            NullLogger<DestinationSessionService>.Instance);

    [Fact]
    public async Task OpenCreatesLayoutHoldsLockAndBindsNewSource()
    {
        var service = CreateService();

        await using var session = await service.OpenAsync(_root, Source, Operator, CancellationToken.None);

        Assert.Equal(DestinationBindingOutcome.BoundNew, session.Binding.Outcome);
        Assert.True(Directory.Exists(session.Destination.ContentRootPath));
        Assert.True(Directory.Exists(session.Destination.StateRootPath));
        Assert.True(File.Exists(session.Destination.StateDatabasePath));
        Assert.True(File.Exists(session.Destination.LockFilePath));
    }

    [Fact]
    public async Task SecondOpenFailsWhileSessionIsHeld()
    {
        var service = CreateService();
        await using var session = await service.OpenAsync(_root, Source, Operator, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.OpenAsync(_root, Source, Operator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.DestinationLocked, exception.ReferenceCode);
    }

    [Fact]
    public async Task ReopenAfterDisposeResumesSameSource()
    {
        var service = CreateService();
        var first = await service.OpenAsync(_root, Source, Operator, CancellationToken.None);
        await first.DisposeAsync();

        await using var second = await service.OpenAsync(_root, Source, Operator, CancellationToken.None);

        Assert.Equal(DestinationBindingOutcome.ResumedExisting, second.Binding.Outcome);
    }

    [Fact]
    public async Task FailedBindingReleasesTheLock()
    {
        var service = CreateService();

        // Bind the second destination to the original source first.
        var bound = await service.OpenAsync(_root + "-other", Source, Operator, CancellationToken.None);
        await bound.DisposeAsync();

        var foreign = Source with { DriveId = "drive-other-999" };
        var exception = await Assert.ThrowsAsync<DestinationException>(
            () => service.OpenAsync(_root + "-other", foreign, Operator, CancellationToken.None));

        Assert.Equal(DestinationErrorCodes.ForeignSourceBinding, exception.ReferenceCode);

        // The failed open released its lock, so the destination can be opened again.
        await using var reopened = await service.OpenAsync(
            _root + "-other", Source, Operator, CancellationToken.None);
        Assert.Equal(DestinationBindingOutcome.ResumedExisting, reopened.Binding.Outcome);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        foreach (var root in new[] { _root, _root + "-other" })
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
