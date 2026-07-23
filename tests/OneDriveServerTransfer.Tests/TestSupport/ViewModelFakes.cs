using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Transfer;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>Shared display-safe test data for the view-model workflow tests.</summary>
internal static class ViewModelTestData
{
    public static ResolvedEmployeeSource DefaultSource() => new(
        "tenant-display-safe",
        "employee-object-display-safe",
        "employee@example.test",
        "Employee Example",
        "drive-display-safe",
        "business",
        "Employee Example",
        "https://tenant-my.sharepoint.test/personal/employee",
        QuotaTotalBytes: null,
        QuotaUsedBytes: null,
        QuotaRemainingBytes: null,
        EmployeeSourceMode.Upn,
        IsTenantConfirmed: true);

    public static ScanResult DefaultScanResult() => new(
        "scan-1",
        FileCount: 3,
        FolderCount: 1,
        EmptyFolderCount: 0,
        UnsupportedCount: 1,
        KnownSourceBytes: 3 * 1024,
        [new UnsupportedScanItem("Notebook", "Notebook", "UnsupportedPackage")],
        [new ScanWarning(ScanWarningKind.PathAdjusted, "An item was given a safe name.", "report?.xlsx")],
        DateTimeOffset.UtcNow);

    public static TransferRunResult RunResult(TransferRunState state, string runId = "run-1") => new(
        runId,
        state,
        CompletedCount: 3,
        SkippedCount: 1,
        FailedCount: state is TransferRunState.Incomplete ? 1 : 0,
        UnsupportedCount: state is TransferRunState.Incomplete ? 1 : 0,
        SourceStable: state is TransferRunState.Completed or TransferRunState.CompletedWithWarnings,
        Warnings: []);

    public static TransferProgress Progress(
        string operation,
        string? currentItemName,
        string? activity,
        long discovered = 5,
        long completed = 2,
        long skipped = 1,
        long unsupported = 1,
        long failed = 0,
        long? totalKnownBytes = 3072,
        long downloadedBytes = 1536) => new(
        operation, currentItemName, activity,
        discovered, completed, skipped, unsupported, failed,
        totalKnownBytes, downloadedBytes);
}

/// <summary>Programmable employee-source-resolver double for view-model tests.</summary>
internal sealed class FakeEmployeeSourceResolver : IEmployeeSourceResolver
{
    public Func<string, CancellationToken, Task<ResolvedEmployeeSource>>? Handler { get; set; }

    public List<string> Inputs { get; } = [];

    public Task<ResolvedEmployeeSource> ResolveAsync(string input, CancellationToken cancellationToken)
    {
        Inputs.Add(input);
        return Handler?.Invoke(input, cancellationToken)
            ?? Task.FromResult(ViewModelTestData.DefaultSource());
    }
}

/// <summary>Programmable destination-session double; sessions wrap a no-op lock.</summary>
internal sealed class FakeDestinationSessionService : IDestinationSessionService
{
    public Func<string, SourceBindingIdentity, CancellationToken, Task<DestinationSession>>? Handler { get; set; }

    public List<string> OpenedPaths { get; } = [];

    public int OpenCallCount => OpenedPaths.Count;

    public Task<DestinationSession> OpenAsync(
        string selectedPath,
        SourceBindingIdentity source,
        OneDriveServerTransfer.Destination.OperatorIdentity operatorIdentity,
        CancellationToken cancellationToken)
    {
        OpenedPaths.Add(selectedPath);
        return Handler?.Invoke(selectedPath, source, cancellationToken)
            ?? Task.FromResult(DestinationSessionFactory.Create(selectedPath));
    }
}

/// <summary>Programmable scan-service double with a controllable currency answer.</summary>
internal sealed class FakeScanService : IScanService
{
    public Func<ResolvedEmployeeSource, DestinationSession, CancellationToken, Task<ScanResult>>? ScanHandler { get; set; }

    public Func<ResolvedEmployeeSource, DestinationSession, CancellationToken, Task<bool>>? IsCurrentHandler { get; set; }

    public int ScanCallCount { get; private set; }

    public int IsCurrentCallCount { get; private set; }

    public Task<ScanResult> ScanAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        ScanCallCount++;
        return ScanHandler?.Invoke(source, session, cancellationToken)
            ?? Task.FromResult(ViewModelTestData.DefaultScanResult());
    }

    public Task<bool> IsScanCurrentAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken)
    {
        IsCurrentCallCount++;
        return IsCurrentHandler?.Invoke(source, session, cancellationToken)
            ?? Task.FromResult(true);
    }
}

/// <summary>Programmable copy-orchestrator double with an optional progress feed.</summary>
internal sealed class FakeTransferOrchestrator : ITransferOrchestrator
{
    public TransferRunResult Result { get; set; } = ViewModelTestData.RunResult(TransferRunState.Completed);

    public Func<ResolvedEmployeeSource, DestinationSession, CancellationToken, IProgress<TransferProgress>?, Task<TransferRunResult>>? Handler { get; set; }

    public IReadOnlyList<TransferProgress>? CannedProgress { get; set; }

    public int CallCount { get; private set; }

    public async Task<TransferRunResult> RunAsync(
        ResolvedEmployeeSource source,
        DestinationSession session,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress = null)
    {
        CallCount++;
        if (Handler is not null)
        {
            return await Handler(source, session, cancellationToken, progress);
        }

        if (CannedProgress is not null)
        {
            foreach (var snapshot in CannedProgress)
            {
                progress?.Report(snapshot);
            }
        }

        return Result;
    }
}

/// <summary>Programmable folder-picker double; null means the operator cancelled.</summary>
internal sealed class FakeFolderPickerService : IFolderPickerService
{
    public string? NextPath { get; set; }

    public int CallCount { get; private set; }

    public string? PickFolder()
    {
        CallCount++;
        return NextPath;
    }
}

/// <summary>Recording shell double with an optional failure.</summary>
internal sealed class FakeShellService : IShellService
{
    public List<string> OpenedFolders { get; } = [];

    public Exception? Failure { get; set; }

    public void OpenFolder(string path)
    {
        OpenedFolders.Add(path);
        if (Failure is not null)
        {
            throw Failure;
        }
    }
}

/// <summary>
/// A synchronization context that runs posted callbacks inline. Progress&lt;T&gt;
/// created while it is installed dispatches synchronously, which makes progress- and
/// activity-order assertions deterministic. Install it only around the trigger, and
/// always restore the previous context.
/// </summary>
internal sealed class ImmediateSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => d(state);

    public static IDisposable Install()
    {
        var previous = Current;
        SetSynchronizationContext(new ImmediateSynchronizationContext());
        return new Restore(previous);
    }

    private sealed class Restore(SynchronizationContext? previous) : IDisposable
    {
        public void Dispose() => SetSynchronizationContext(previous);
    }
}

/// <summary>Bundle of the view-model's service doubles with a factory helper.</summary>
internal sealed class ViewModelTestRig
{
    public FakeAuthenticationService Authentication { get; } = new();

    public FakeEmployeeSourceResolver SourceResolver { get; } = new();

    public FakeDestinationSessionService DestinationSessions { get; } = new();

    public FakeScanService Scan { get; } = new();

    public FakeTransferOrchestrator Orchestrator { get; } = new();

    public FakeFolderPickerService FolderPicker { get; } = new();

    public FakeShellService Shell { get; } = new();

    public MainViewModel CreateViewModel(bool rememberSignInDefault = true) => new(
        Authentication,
        Options.Create(new AuthenticationOptions { RememberSignInDefault = rememberSignInDefault }),
        SourceResolver,
        DestinationSessions,
        Scan,
        Orchestrator,
        FolderPicker,
        Shell,
        NullLogger<MainViewModel>.Instance);

    public static async Task WaitForAsync(Func<bool> condition, string? because = null)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), because ?? "The expected view-model state was not reached in time.");
    }
}
