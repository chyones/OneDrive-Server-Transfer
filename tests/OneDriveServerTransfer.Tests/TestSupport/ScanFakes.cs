using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>
/// Programmable delta client double for scan tests. Serves queued pages through the
/// sink in order and records the resume link of every enumeration.
/// </summary>
internal sealed class FakeDeltaInventoryClient : IDeltaInventoryClient
{
    private readonly Queue<DeltaInventoryPage> _pages = new();

    public List<string?> ResumeLinks { get; } = [];

    public int EnumerateCallCount { get; private set; }

    /// <summary>Optional failure raised after <see cref="FailAfterPages" /> pages.</summary>
    public Exception? Failure { get; set; }

    public int FailAfterPages { get; set; } = int.MaxValue;

    public void EnqueuePage(DeltaInventoryPage page) => _pages.Enqueue(page);

    public async Task<DeltaEnumerationResult> EnumerateAsync(
        string driveId,
        string? resumeLink,
        Func<DeltaInventoryPage, CancellationToken, Task> pageSink,
        CancellationToken cancellationToken)
    {
        EnumerateCallCount++;
        ResumeLinks.Add(resumeLink);

        long pageCount = 0;
        long itemCount = 0;
        string? checkpoint = null;

        while (_pages.Count > 0)
        {
            // FailAfterPages = 0 fails before the first page of this enumeration;
            // FailAfterPages = N fails after N pages were applied through the sink.
            if (pageCount >= FailAfterPages && Failure is not null)
            {
                throw Failure;
            }

            var page = _pages.Dequeue();
            await pageSink(page, cancellationToken);
            pageCount++;
            itemCount += page.Items.Count;

            if (page.IsFinal)
            {
                checkpoint = page.DeltaLink;
                break;
            }
        }

        return new DeltaEnumerationResult(
            checkpoint ?? "fake-checkpoint", pageCount, itemCount);
    }
}

/// <summary>Recording binding-service double; binding behavior itself is M4-tested.</summary>
internal sealed class FakeDestinationBindingService : IDestinationBindingService
{
    public int CallCount { get; private set; }

    public SourceBindingIdentity? LastSource { get; private set; }

    public Task<DestinationBindingResult> BindOrValidateAsync(
        ResolvedDestination destination,
        SourceBindingIdentity source,
        OneDriveServerTransfer.Destination.OperatorIdentity operatorIdentity,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastSource = source;
        return Task.FromResult(new DestinationBindingResult(DestinationBindingOutcome.ResumedExisting));
    }
}

/// <summary>Programmable security-evaluator double.</summary>
internal sealed class FakeDestinationSecurityEvaluator : IDestinationSecurityEvaluator
{
    public DestinationSecurityAssessment Assessment { get; set; } = DestinationSecurityAssessment.Clear;

    public DestinationSecurityAssessment Evaluate(ResolvedDestination destination) => Assessment;
}

/// <summary>No-op destination lock double for building sessions in tests.</summary>
internal sealed class FakeDestinationLock : IDestinationLock
{
    public FakeDestinationLock(ResolvedDestination destination) => Destination = destination;

    public ResolvedDestination Destination { get; }

    public string DestinationRoot => Destination.RootPath;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Builds an open <see cref="DestinationSession" /> over a temp destination root.</summary>
internal static class DestinationSessionFactory
{
    public static DestinationSession Create(string rootPath)
    {
        var destination = new ResolvedDestination(rootPath);
        return new DestinationSession(
            destination,
            new DestinationBindingResult(DestinationBindingOutcome.BoundNew),
            new FakeDestinationLock(destination));
    }
}
