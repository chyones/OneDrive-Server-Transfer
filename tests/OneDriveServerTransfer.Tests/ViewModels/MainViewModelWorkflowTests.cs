using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Reporting;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;
using OneDriveServerTransfer.Transfer;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer.Tests.ViewModels;

/// <summary>
/// The contract section 2 workflow as UI state: sign-in, source input, destination,
/// mandatory scan, explicit confirmation, copy with progress, cancellation, exact
/// terminal states, and Open Report / Open Destination.
/// </summary>
public class MainViewModelWorkflowTests
{
    private const string Destination = "D:\\Archive";

    private static async Task<MainViewModel> SignedInViewModelAsync(ViewModelTestRig rig)
    {
        var viewModel = rig.CreateViewModel();
        viewModel.SignInCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.IsSignedIn);
        return viewModel;
    }

    private static async Task<MainViewModel> ScannedViewModelAsync(ViewModelTestRig rig)
    {
        var viewModel = await SignedInViewModelAsync(rig);
        viewModel.SourceInput = "employee@example.test";
        rig.FolderPicker.NextPath = Destination;
        viewModel.BrowseDestinationCommand.Execute(null);
        Assert.Equal(Destination, viewModel.DestinationPath);

        Assert.True(viewModel.ScanCommand.CanExecute(null));
        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasScanSummary);
        return viewModel;
    }

    [Fact]
    public async Task FullWorkflowReachesTerminalCompletedState()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await ScannedViewModelAsync(rig);

        // The dry-run summary shows display identities only and never claims copying.
        Assert.Equal("Employee Example (employee@example.test)", viewModel.ScanEmployeeDisplay);
        Assert.Equal("Test Operator (operator@example.test)", viewModel.ScanOperatorDisplay);
        Assert.Equal("Employee UPN", viewModel.ScanSourceModeDisplay);
        Assert.Equal(Destination, viewModel.ScanDestinationDisplay);
        Assert.Equal("3", viewModel.ScanFileCountText);
        Assert.Equal("3.0 KB", viewModel.ScanKnownSizeText);
        Assert.Equal("1", viewModel.ScanUnsupportedCountText);
        Assert.Equal("1", viewModel.ScanPathWarningCountText);
        Assert.Equal("0", viewModel.ScanStorageWarningCountText);
        Assert.Single(viewModel.ScanWarnings);
        Assert.Contains("No content has been copied", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null)); // confirmation still missing

        viewModel.IsScanConfirmed = true;
        Assert.True(viewModel.StartCopyCommand.CanExecute(null));
        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);

        Assert.Equal(1, rig.Orchestrator.CallCount);
        Assert.Equal(TransferRunState.Completed, viewModel.FinalRunState);
        Assert.Equal("Completed", viewModel.FinalStateName);
        Assert.Equal("Copy completed", viewModel.FinalStateTitle);
        Assert.Equal(3, viewModel.CompletedCount);
        Assert.Equal(1, viewModel.SkippedCount);
        Assert.True(viewModel.OpenReportCommand.CanExecute(null));
        Assert.Equal(
            ReportWriter.GetRunReportDirectoryPath(Destination, "run-1"),
            viewModel.ReportDirectoryPath);
    }

    [Fact]
    public async Task EditingSourceInvalidatesScanAndDisablesStartCopy()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;
        Assert.True(viewModel.StartCopyCommand.CanExecute(null));

        viewModel.SourceInput = "other@example.test";

        Assert.False(viewModel.HasScanSummary);
        Assert.False(viewModel.IsScanConfirmed);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null));
        Assert.Contains("source changed", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangingDestinationInvalidatesScanAndDisablesStartCopy()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        viewModel.DestinationPath = "E:\\Other";

        Assert.False(viewModel.HasScanSummary);
        Assert.False(viewModel.IsScanConfirmed);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null));
        Assert.Contains("destination changed", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartCopyStaysDisabledWithoutScanOrConfirmation()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await SignedInViewModelAsync(rig);

        // No scan at all.
        Assert.False(viewModel.StartCopyCommand.CanExecute(null));

        // Scan succeeded but the confirmation is missing.
        viewModel.SourceInput = "employee@example.test";
        viewModel.DestinationPath = Destination;
        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasScanSummary);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null));

        // Confirmation without a scan summary is rejected by the setter.
        var withoutSummary = await SignedInViewModelAsync(new ViewModelTestRig());
        withoutSummary.IsScanConfirmed = true;
        Assert.False(withoutSummary.IsScanConfirmed);
    }

    [Fact]
    public async Task StartCopyRevalidatesCurrencyAndRejectsStaleScan()
    {
        var rig = new ViewModelTestRig();
        rig.Scan.IsCurrentHandler = (_, _, _) => Task.FromResult(false);
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasError);

        Assert.Equal(TransferErrorCodes.ScanNotCurrent, viewModel.ErrorReferenceCode);
        Assert.Equal(1, rig.Scan.IsCurrentCallCount);
        Assert.Equal(0, rig.Orchestrator.CallCount);
        Assert.False(viewModel.HasScanSummary);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task ProgressUpdatesSurfaceCountsCurrentFileAndPercentage()
    {
        var rig = new ViewModelTestRig();
        var gate = new TaskCompletionSource<TransferRunResult>();
        rig.Orchestrator.Handler = (_, _, _, progress) =>
        {
            progress?.Report(ViewModelTestData.Progress(
                "Copying files", "report.xlsx", "Copied report.xlsx"));
            return gate.Task;
        };
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        using var context = ImmediateSynchronizationContext.Install();
        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.CurrentItemName == "report.xlsx");

        Assert.Equal("Copying files", viewModel.CurrentOperation);
        Assert.Equal(5, viewModel.DiscoveredCount);
        Assert.Equal(2, viewModel.CompletedCount);
        Assert.Equal(1, viewModel.SkippedCount);
        Assert.Equal(1, viewModel.UnsupportedCount);
        Assert.Equal(0, viewModel.FailedCount);
        Assert.Equal(1536, viewModel.DownloadedBytes);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(50.0, viewModel.ProgressPercent);
        Assert.Contains("Copied report.xlsx", viewModel.RecentActivity);

        gate.SetResult(ViewModelTestData.RunResult(TransferRunState.Completed));
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);
    }

    [Fact]
    public async Task ProgressStaysIndeterminateWhileTotalsAreUnknown()
    {
        var rig = new ViewModelTestRig();
        rig.Scan.ScanHandler = (_, _, _) => Task.FromResult(
            ViewModelTestData.DefaultScanResult() with { KnownSourceBytes = 0 });
        var gate = new TaskCompletionSource<TransferRunResult>();
        rig.Orchestrator.Handler = (_, _, _, progress) =>
        {
            progress?.Report(ViewModelTestData.Progress(
                "Copying files", "data.bin", null, totalKnownBytes: null, downloadedBytes: 512));
            return gate.Task;
        };
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        using var context = ImmediateSynchronizationContext.Install();
        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.CurrentItemName == "data.bin");

        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressPercent);
        Assert.Equal("0 bytes", viewModel.TotalKnownSizeText); // the scan known total is zero

        gate.SetResult(ViewModelTestData.RunResult(TransferRunState.Completed));
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);
    }

    [Fact]
    public async Task TotalSizeShowsUnknownBeforeAnyScan()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await SignedInViewModelAsync(rig);

        Assert.Null(viewModel.TotalKnownBytes);
        Assert.Equal("Unknown", viewModel.TotalKnownSizeText);
        Assert.True(viewModel.IsProgressIndeterminate);
    }

    [Fact]
    public async Task RecentActivityListIsBoundedAndDropsOldestEntries()
    {
        var rig = new ViewModelTestRig();
        rig.Orchestrator.CannedProgress = Enumerable
            .Range(1, 150)
            .Select(index => ViewModelTestData.Progress(
                "Copying files", $"file{index}", $"Copied file{index}"))
            .ToArray();
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        using var context = ImmediateSynchronizationContext.Install();
        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);

        // 1 "Copy started." + 150 file entries + 1 "Run finished." = 152, bounded to 100.
        Assert.Equal(MainViewModel.MaxActivityEntries, viewModel.RecentActivity.Count);
        Assert.Equal("Copied file52", viewModel.RecentActivity[0]);
        Assert.Equal("Run finished: Completed.", viewModel.RecentActivity[^1]);
    }

    [Fact]
    public async Task CancelDuringScanStopsTheScan()
    {
        var rig = new ViewModelTestRig();
        rig.Scan.ScanHandler = async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ViewModelTestData.DefaultScanResult();
        };
        var viewModel = await SignedInViewModelAsync(rig);
        viewModel.SourceInput = "employee@example.test";
        viewModel.DestinationPath = Destination;

        using var context = ImmediateSynchronizationContext.Install();
        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.IsScanning);
        Assert.True(viewModel.CancelCommand.CanExecute(null));

        viewModel.CancelCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => !viewModel.IsScanning);

        Assert.Contains("cancelled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.HasScanSummary);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancelDuringCopyShowsCancelledTerminalState()
    {
        var rig = new ViewModelTestRig();
        rig.Orchestrator.Handler = async (_, _, cancellationToken, _) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ViewModelTestData.RunResult(TransferRunState.Completed);
        };
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        using var context = ImmediateSynchronizationContext.Install();
        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.IsCopying);

        viewModel.CancelCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);

        Assert.Equal("Cancelled", viewModel.FinalStateName);
        Assert.Contains("not complete", viewModel.FinalStateExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null)); // a fresh scan is required
    }

    [Theory]
    [InlineData(TransferRunState.Completed, "Copy completed")]
    [InlineData(TransferRunState.CompletedWithWarnings, "Copy completed with warnings")]
    [InlineData(TransferRunState.Incomplete, "The archive is NOT complete")]
    [InlineData(TransferRunState.Failed, "The copy failed")]
    [InlineData(TransferRunState.Cancelled, "The copy was cancelled")]
    [InlineData(TransferRunState.Interrupted, "The copy was interrupted")]
    public async Task TerminalStatesDisplayExactNamesAndTitles(TransferRunState state, string expectedTitle)
    {
        var rig = new ViewModelTestRig();
        rig.Orchestrator.Result = ViewModelTestData.RunResult(state);
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);

        Assert.Equal(state, viewModel.FinalRunState);
        Assert.Equal(state.ToString(), viewModel.FinalStateName);
        Assert.Equal(expectedTitle, viewModel.FinalStateTitle);
        if (state is not (TransferRunState.Completed or TransferRunState.CompletedWithWarnings))
        {
            Assert.Contains("not complete", viewModel.FinalStateExplanation, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task IncompleteStatesClearlyThatTheArchiveIsNotComplete()
    {
        var rig = new ViewModelTestRig();
        rig.Orchestrator.Result = ViewModelTestData.RunResult(TransferRunState.Incomplete);
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);

        Assert.Contains("NOT complete", viewModel.FinalStateExplanation, StringComparison.Ordinal);
        Assert.Equal(1, viewModel.FailedCount);
        Assert.Equal(1, viewModel.UnsupportedCount);
    }

    [Fact]
    public async Task OpenReportOpensTheRunReportDirectoryThroughTheShell()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await SignedInViewModelAsync(rig);

        Assert.False(viewModel.OpenReportCommand.CanExecute(null)); // no finished run

        viewModel.SourceInput = "employee@example.test";
        viewModel.DestinationPath = Destination;
        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasScanSummary);
        Assert.False(viewModel.OpenReportCommand.CanExecute(null)); // scan alone is not a run

        viewModel.IsScanConfirmed = true;
        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);

        var expected = ReportWriter.GetRunReportDirectoryPath(Destination, "run-1");
        Assert.Equal(expected, viewModel.ReportDirectoryPath);
        Assert.True(viewModel.OpenReportCommand.CanExecute(null));

        viewModel.OpenReportCommand.Execute(null);
        Assert.Equal([expected], rig.Shell.OpenedFolders);
    }

    [Fact]
    public async Task OpenDestinationOpensTheSelectedDestinationThroughTheShell()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await SignedInViewModelAsync(rig);

        Assert.False(viewModel.OpenDestinationCommand.CanExecute(null)); // nothing selected

        rig.FolderPicker.NextPath = Destination;
        viewModel.BrowseDestinationCommand.Execute(null);
        Assert.True(viewModel.OpenDestinationCommand.CanExecute(null));

        viewModel.OpenDestinationCommand.Execute(null);
        Assert.Equal([Destination], rig.Shell.OpenedFolders);
    }

    [Fact]
    public async Task ShellFailureShowsReferenceCodedError()
    {
        var rig = new ViewModelTestRig();
        rig.Shell.Failure = UserInterfaceErrors.ShellOpenFailed();
        var viewModel = await SignedInViewModelAsync(rig);
        viewModel.DestinationPath = Destination;

        viewModel.OpenDestinationCommand.Execute(null);

        Assert.True(viewModel.HasError);
        Assert.Equal(UserInterfaceErrorCodes.ShellOpenFailed, viewModel.ErrorReferenceCode);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorTitle));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorAction));
    }

    [Fact]
    public async Task SignOutClearsTheWorkflowState()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;
        Assert.True(viewModel.StartCopyCommand.CanExecute(null));

        viewModel.SignOutCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => !viewModel.IsSignedIn);

        Assert.False(viewModel.HasScanSummary);
        Assert.False(viewModel.IsScanConfirmed);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null));
        Assert.False(viewModel.ScanCommand.CanExecute(null)); // sign-in required again
        Assert.Equal(OneDriveServerTransfer.Authentication.AuthenticationErrors.SignOutDescription, viewModel.StatusMessage);
    }

    [Fact]
    public async Task SignOutIsDisabledWhileCopyRuns()
    {
        var rig = new ViewModelTestRig();
        var gate = new TaskCompletionSource<TransferRunResult>();
        rig.Orchestrator.Handler = (_, _, _, _) => gate.Task;
        var viewModel = await ScannedViewModelAsync(rig);
        viewModel.IsScanConfirmed = true;

        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.IsCopying);

        Assert.False(viewModel.SignOutCommand.CanExecute(null));
        Assert.False(viewModel.ScanCommand.CanExecute(null));
        Assert.False(viewModel.BrowseDestinationCommand.CanExecute(null));
        Assert.True(viewModel.CancelCommand.CanExecute(null));

        gate.SetResult(ViewModelTestData.RunResult(TransferRunState.Completed));
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasFinalResult);
    }
}
