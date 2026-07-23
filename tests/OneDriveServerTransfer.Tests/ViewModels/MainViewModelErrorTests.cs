using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Tests.TestSupport;
using OneDriveServerTransfer.Transfer;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer.Tests.ViewModels;

/// <summary>
/// User-facing error shape and redaction: every error has a title, plain-language
/// explanation, corrective action, and stable reference code, and protected values
/// (tenant IDs, drive IDs, tokens, temporary URLs, authorization headers) never
/// reach any UI text.
/// </summary>
public class MainViewModelErrorTests
{
    private const string Destination = "D:\\Archive";

    private static readonly string[] ProtectedVectors =
    [
        "11111111-2222-3333-4444-555555555555", // tenant ID shape
        "b!aBcDeFgHiJkLmNoPqRsTuV",             // drive ID shape
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.c2ln", // JWT
        "https://download.temp.example/abc?sig=secret-value",         // temporary URL
        "Authorization: Bearer abcdef",
        "client_secret=hunter2",
    ];

    private static async Task<MainViewModel> ScanReadyViewModelAsync(ViewModelTestRig rig)
    {
        var viewModel = rig.CreateViewModel();
        viewModel.SignInCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.IsSignedIn);
        viewModel.SourceInput = "employee@example.test";
        viewModel.DestinationPath = Destination;
        return viewModel;
    }

    private static void AssertNoProtectedText(MainViewModel viewModel)
    {
        var visibleText = string.Join('\n',
            viewModel.ErrorTitle,
            viewModel.ErrorMessage,
            viewModel.ErrorAction,
            viewModel.StatusMessage,
            viewModel.ScanEmployeeDisplay,
            viewModel.ScanOperatorDisplay,
            viewModel.FinalStateTitle,
            viewModel.FinalStateExplanation);

        foreach (var vector in ProtectedVectors)
        {
            Assert.DoesNotContain(vector, visibleText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SourceResolutionErrorSurfacesAllFields()
    {
        var rig = new ViewModelTestRig();
        rig.SourceResolver.Handler = (_, _) => Task.FromException<ResolvedEmployeeSource>(
            new SourceResolutionException(
                "SRC-TST-001", "The employee could not be resolved",
                "The entered value is not a valid employee UPN or OneDrive root URL.",
                "Check the value and try again."));
        var viewModel = await ScanReadyViewModelAsync(rig);

        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasError);

        Assert.Equal("SRC-TST-001", viewModel.ErrorReferenceCode);
        Assert.Equal("The employee could not be resolved", viewModel.ErrorTitle);
        Assert.Equal("The entered value is not a valid employee UPN or OneDrive root URL.", viewModel.ErrorMessage);
        Assert.Equal("Check the value and try again.", viewModel.ErrorAction);
        Assert.False(viewModel.HasScanSummary);
    }

    [Fact]
    public async Task DestinationErrorSurfacesReferenceCode()
    {
        var rig = new ViewModelTestRig();
        rig.DestinationSessions.Handler = (_, _, _) => Task.FromException<DestinationSession>(
            DestinationErrors.InvalidStateDatabase());
        var viewModel = await ScanReadyViewModelAsync(rig);

        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasError);

        Assert.Equal(DestinationErrorCodes.InvalidStateDatabase, viewModel.ErrorReferenceCode);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorTitle));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorAction));
    }

    [Fact]
    public async Task ScanErrorSurfacesReferenceCode()
    {
        var rig = new ViewModelTestRig();
        rig.Scan.ScanHandler = (_, _, _) => Task.FromException<ScanResult>(ScanErrors.SourceAccessDenied());
        var viewModel = await ScanReadyViewModelAsync(rig);

        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasError);

        Assert.Equal(ScanErrorCodes.SourceAccessDenied, viewModel.ErrorReferenceCode);
        Assert.False(viewModel.HasScanSummary);
    }

    [Fact]
    public async Task CopyErrorSurfacesReferenceCode()
    {
        var rig = new ViewModelTestRig();
        rig.Orchestrator.Handler = (_, _, _, _) => Task.FromException<TransferRunResult>(
            TransferErrors.InsufficientStorage());
        var viewModel = await ScanReadyViewModelAsync(rig);
        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasScanSummary);
        viewModel.IsScanConfirmed = true;

        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasError);

        Assert.Equal(TransferErrorCodes.InsufficientStorage, viewModel.ErrorReferenceCode);
        Assert.False(viewModel.HasFinalResult);
        Assert.False(viewModel.StartCopyCommand.CanExecute(null)); // fresh scan required
    }

    [Fact]
    public async Task UnexpectedScanFailureIsWrappedAndRedacted()
    {
        var rig = new ViewModelTestRig();
        rig.SourceResolver.Handler = (_, _) => Task.FromException<ResolvedEmployeeSource>(
            new InvalidOperationException(
                "graph failed for tenant 11111111-2222-3333-4444-555555555555 " +
                "drive b!aBcDeFgHiJkLmNoPqRsTuV " +
                "token eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.c2ln " +
                "url https://download.temp.example/abc?sig=secret-value " +
                "Authorization: Bearer abcdef client_secret=hunter2"));
        var viewModel = await ScanReadyViewModelAsync(rig);

        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasError);

        Assert.Equal(UserInterfaceErrorCodes.Unexpected, viewModel.ErrorReferenceCode);
        AssertNoProtectedText(viewModel);
    }

    [Fact]
    public async Task UnexpectedCopyFailureIsWrappedAndRedacted()
    {
        var rig = new ViewModelTestRig();
        rig.Orchestrator.Handler = (_, _, _, _) => Task.FromException<TransferRunResult>(
            new InvalidOperationException(
                "tenant 11111111-2222-3333-4444-555555555555 drive b!aBcDeFgHiJkLmNoPqRsTuV " +
                "https://download.temp.example/abc?sig=secret-value Authorization: Bearer abcdef"));
        var viewModel = await ScanReadyViewModelAsync(rig);
        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasScanSummary);
        viewModel.IsScanConfirmed = true;

        viewModel.StartCopyCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasError);

        Assert.Equal(UserInterfaceErrorCodes.Unexpected, viewModel.ErrorReferenceCode);
        AssertNoProtectedText(viewModel);
    }

    [Fact]
    public async Task ScanSummaryNeverShowsProtectedIdentifiers()
    {
        var rig = new ViewModelTestRig();
        var viewModel = await ScanReadyViewModelAsync(rig);

        viewModel.ScanCommand.Execute(null);
        await ViewModelTestRig.WaitForAsync(() => viewModel.HasScanSummary);

        // The resolved source carries durable identifiers; none may reach the summary.
        Assert.DoesNotContain("drive-display-safe", viewModel.ScanEmployeeDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant-display-safe", viewModel.ScanOperatorDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("employee-object-display-safe", viewModel.ScanEmployeeDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("drive-display-safe", viewModel.ScanDestinationDisplay, StringComparison.Ordinal);

        // The summary must never claim content was copied.
        Assert.Contains("No content has been copied", viewModel.StatusMessage, StringComparison.Ordinal);
    }
}
