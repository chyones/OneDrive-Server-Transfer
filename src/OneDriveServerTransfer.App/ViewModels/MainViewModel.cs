using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Reporting;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Transfer;
using OperatorIdentity = OneDriveServerTransfer.Abstractions.OperatorIdentity;

namespace OneDriveServerTransfer.ViewModels;

/// <summary>
/// View model for the single application window: the complete contract section 2
/// workflow. Sign-in (M2), one employee source input (M3), one local destination
/// (M4), the mandatory dry-run scan (M5), explicit confirmation, copy execution with
/// advisory progress (M5/M6), cancellation, exact terminal run states, and Open
/// Report / Open Destination. Every error surface carries a short title,
/// plain-language explanation, corrective action, and stable reference code, and
/// never exposes secrets, identifiers, URLs, or stack traces.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    /// <summary>Upper bound of the recent-activity list; the oldest entries drop off first.</summary>
    public const int MaxActivityEntries = 100;

    private const string ScanCompleteStatus =
        "Scan complete. Review the summary, confirm it, and then start the copy. No content has been copied.";

    private const string ScanCancelledStatus =
        "The scan was cancelled. Run the scan again when you are ready.";

    private readonly IAuthenticationService _authenticationService;
    private readonly IEmployeeSourceResolver _sourceResolver;
    private readonly IDestinationSessionService _destinationSessionService;
    private readonly IScanService _scanService;
    private readonly ITransferOrchestrator _transferOrchestrator;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IShellService _shellService;
    private readonly ILogger<MainViewModel> _logger;

    private OperatorIdentity? _operatorIdentity;
    private ResolvedEmployeeSource? _resolvedSource;
    private DestinationSession? _session;
    private bool _scanIsCurrent;
    private CancellationTokenSource? _operationCts;

    private bool _isBusy;
    private bool _isSignedIn;
    private bool _rememberSignIn;
    private string? _operatorDisplayName;
    private string? _operatorUpn;
    private string _statusMessage = "Sign in with the authorized IT transfer account to continue.";
    private string? _errorTitle;
    private string? _errorMessage;
    private string? _errorAction;
    private string? _errorReferenceCode;

    private string _sourceInput = string.Empty;
    private string _destinationPath = string.Empty;
    private bool _isScanning;
    private bool _isCopying;
    private bool _isScanConfirmed;

    private bool _hasScanSummary;
    private string? _scanEmployeeDisplay;
    private string? _scanOperatorDisplay;
    private string? _scanSourceModeDisplay;
    private string? _scanDestinationDisplay;
    private string? _scanFileCountText;
    private string? _scanKnownSizeText;
    private string? _scanUnsupportedCountText;
    private string? _scanPathWarningCountText;
    private string? _scanStorageWarningCountText;

    private string? _currentOperation;
    private string? _currentItemName;
    private long _discoveredCount;
    private long _completedCount;
    private long _skippedCount;
    private long _unsupportedCount;
    private long _failedCount;
    private long _downloadedBytes;
    private long? _totalKnownBytes;
    private double _progressPercent;
    private bool _isProgressIndeterminate = true;

    private bool _hasFinalResult;
    private TransferRunState? _finalRunState;
    private string? _finalStateName;
    private string? _finalStateTitle;
    private string? _finalStateExplanation;
    private string? _reportDirectoryPath;

    public MainViewModel(
        IAuthenticationService authenticationService,
        IOptions<AuthenticationOptions> options,
        IEmployeeSourceResolver sourceResolver,
        IDestinationSessionService destinationSessionService,
        IScanService scanService,
        ITransferOrchestrator transferOrchestrator,
        IFolderPickerService folderPickerService,
        IShellService shellService,
        ILogger<MainViewModel> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _sourceResolver = sourceResolver ?? throw new ArgumentNullException(nameof(sourceResolver));
        _destinationSessionService = destinationSessionService ?? throw new ArgumentNullException(nameof(destinationSessionService));
        _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
        _transferOrchestrator = transferOrchestrator ?? throw new ArgumentNullException(nameof(transferOrchestrator));
        _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
        _shellService = shellService ?? throw new ArgumentNullException(nameof(shellService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            _rememberSignIn = options.Value.RememberSignInDefault;
        }
        catch (OptionsValidationException)
        {
            // Configuration is validated fail-safe at sign-in; the shell still opens.
            _rememberSignIn = true;
        }

        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !IsOperationRunning && !IsSignedIn);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync, () => !IsOperationRunning && IsSignedIn);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination, () => !IsOperationRunning);
        ScanCommand = new AsyncRelayCommand(ScanAsync,
            () => IsSignedIn && !IsOperationRunning &&
                  !string.IsNullOrWhiteSpace(SourceInput) && !string.IsNullOrWhiteSpace(DestinationPath));
        StartCopyCommand = new AsyncRelayCommand(StartCopyAsync,
            () => IsSignedIn && !IsOperationRunning && _scanIsCurrent && IsScanConfirmed);
        CancelCommand = new RelayCommand(CancelOperation, () => IsScanning || IsCopying);
        OpenReportCommand = new RelayCommand(OpenReport, () => CanOpenReport);
        OpenDestinationCommand = new RelayCommand(OpenDestination,
            () => !string.IsNullOrWhiteSpace(DestinationPath));
    }

    /// <summary>Provides the owner window handle for the interactive sign-in flow.</summary>
    public Func<IntPtr>? WindowHandleProvider { get; set; }

    public string WindowTitle => "OneDrive Server Transfer";

    public AsyncRelayCommand SignInCommand { get; }

    public AsyncRelayCommand SignOutCommand { get; }

    public RelayCommand BrowseDestinationCommand { get; }

    public AsyncRelayCommand ScanCommand { get; }

    public AsyncRelayCommand StartCopyCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand OpenReportCommand { get; }

    public RelayCommand OpenDestinationCommand { get; }

    public ObservableCollection<string> ScanWarnings { get; } = [];

    public ObservableCollection<string> RecentActivity { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnOperationStateChanged();
            }
        }
    }

    public bool IsSignedIn
    {
        get => _isSignedIn;
        private set
        {
            if (SetProperty(ref _isSignedIn, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool RememberSignIn
    {
        get => _rememberSignIn;
        set => SetProperty(ref _rememberSignIn, value);
    }

    public string? OperatorDisplayName
    {
        get => _operatorDisplayName;
        private set => SetProperty(ref _operatorDisplayName, value);
    }

    public string? OperatorUpn
    {
        get => _operatorUpn;
        private set => SetProperty(ref _operatorUpn, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorTitle
    {
        get => _errorTitle;
        private set => SetProperty(ref _errorTitle, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? ErrorAction
    {
        get => _errorAction;
        private set => SetProperty(ref _errorAction, value);
    }

    public string? ErrorReferenceCode
    {
        get => _errorReferenceCode;
        private set => SetProperty(ref _errorReferenceCode, value);
    }

    public bool HasError => ErrorReferenceCode is not null;

    /// <summary>The one employee source input: a UPN or a OneDrive for Business root URL.</summary>
    public string SourceInput
    {
        get => _sourceInput;
        set
        {
            if (SetProperty(ref _sourceInput, value))
            {
                InvalidateScan("The employee source changed. Run the scan again to enable copying.");
                RefreshCommands();
            }
        }
    }

    /// <summary>The selected local destination root.</summary>
    public string DestinationPath
    {
        get => _destinationPath;
        set
        {
            if (SetProperty(ref _destinationPath, value))
            {
                InvalidateScan("The destination changed. Run the scan again to enable copying.");
                RefreshCommands();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnOperationStateChanged();
            }
        }
    }

    public bool IsCopying
    {
        get => _isCopying;
        private set
        {
            if (SetProperty(ref _isCopying, value))
            {
                OnOperationStateChanged();
            }
        }
    }

    /// <summary>True while any blocking operation (sign-in, scan, or copy) runs.</summary>
    public bool IsOperationRunning => IsBusy || IsScanning || IsCopying;

    /// <summary>
    /// The explicit operator confirmation required before copying. It resets whenever
    /// the scan is invalidated and can only be set while a successful scan summary is
    /// shown.
    /// </summary>
    public bool IsScanConfirmed
    {
        get => _isScanConfirmed;
        set
        {
            if (SetProperty(ref _isScanConfirmed, value && HasScanSummary))
            {
                RefreshCommands();
            }
        }
    }

    public bool HasScanSummary
    {
        get => _hasScanSummary;
        private set
        {
            if (SetProperty(ref _hasScanSummary, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>Resolved employee display identity (display name and UPN; never IDs).</summary>
    public string? ScanEmployeeDisplay
    {
        get => _scanEmployeeDisplay;
        private set => SetProperty(ref _scanEmployeeDisplay, value);
    }

    public string? ScanOperatorDisplay
    {
        get => _scanOperatorDisplay;
        private set => SetProperty(ref _scanOperatorDisplay, value);
    }

    public string? ScanSourceModeDisplay
    {
        get => _scanSourceModeDisplay;
        private set => SetProperty(ref _scanSourceModeDisplay, value);
    }

    public string? ScanDestinationDisplay
    {
        get => _scanDestinationDisplay;
        private set => SetProperty(ref _scanDestinationDisplay, value);
    }

    public string? ScanFileCountText
    {
        get => _scanFileCountText;
        private set => SetProperty(ref _scanFileCountText, value);
    }

    public string? ScanKnownSizeText
    {
        get => _scanKnownSizeText;
        private set => SetProperty(ref _scanKnownSizeText, value);
    }

    public string? ScanUnsupportedCountText
    {
        get => _scanUnsupportedCountText;
        private set => SetProperty(ref _scanUnsupportedCountText, value);
    }

    public string? ScanPathWarningCountText
    {
        get => _scanPathWarningCountText;
        private set => SetProperty(ref _scanPathWarningCountText, value);
    }

    public string? ScanStorageWarningCountText
    {
        get => _scanStorageWarningCountText;
        private set => SetProperty(ref _scanStorageWarningCountText, value);
    }

    /// <summary>True while the scan, copy, or terminal progress area should be visible.</summary>
    public bool ShowProgressArea => IsScanning || IsCopying || HasFinalResult;

    public string? CurrentOperation
    {
        get => _currentOperation;
        private set => SetProperty(ref _currentOperation, value);
    }

    public string? CurrentItemName
    {
        get => _currentItemName;
        private set => SetProperty(ref _currentItemName, value);
    }

    public long DiscoveredCount
    {
        get => _discoveredCount;
        private set => SetProperty(ref _discoveredCount, value);
    }

    public long CompletedCount
    {
        get => _completedCount;
        private set => SetProperty(ref _completedCount, value);
    }

    public long SkippedCount
    {
        get => _skippedCount;
        private set => SetProperty(ref _skippedCount, value);
    }

    public long UnsupportedCount
    {
        get => _unsupportedCount;
        private set => SetProperty(ref _unsupportedCount, value);
    }

    public long FailedCount
    {
        get => _failedCount;
        private set => SetProperty(ref _failedCount, value);
    }

    public long DownloadedBytes
    {
        get => _downloadedBytes;
        private set
        {
            if (SetProperty(ref _downloadedBytes, value))
            {
                OnPropertyChanged(nameof(DownloadedSizeText));
            }
        }
    }

    public string DownloadedSizeText => FormatBytes(DownloadedBytes);

    /// <summary>Known total source bytes; null while the total is unknown.</summary>
    public long? TotalKnownBytes
    {
        get => _totalKnownBytes;
        private set
        {
            if (SetProperty(ref _totalKnownBytes, value))
            {
                OnPropertyChanged(nameof(TotalKnownSizeText));
            }
        }
    }

    public string TotalKnownSizeText => TotalKnownBytes is { } total ? FormatBytes(total) : "Unknown";

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    /// <summary>True while totals are unknown or the scan runs; the UI never fabricates a percentage.</summary>
    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public bool HasFinalResult
    {
        get => _hasFinalResult;
        private set
        {
            if (SetProperty(ref _hasFinalResult, value))
            {
                OnPropertyChanged(nameof(ShowProgressArea));
                RefreshCommands();
            }
        }
    }

    /// <summary>The exact approved terminal run state of the last copy run.</summary>
    public TransferRunState? FinalRunState
    {
        get => _finalRunState;
        private set => SetProperty(ref _finalRunState, value);
    }

    /// <summary>The exact approved run-state name (for example CompletedWithWarnings).</summary>
    public string? FinalStateName
    {
        get => _finalStateName;
        private set => SetProperty(ref _finalStateName, value);
    }

    public string? FinalStateTitle
    {
        get => _finalStateTitle;
        private set => SetProperty(ref _finalStateTitle, value);
    }

    public string? FinalStateExplanation
    {
        get => _finalStateExplanation;
        private set => SetProperty(ref _finalStateExplanation, value);
    }

    /// <summary>The finished run's report directory; null until reports exist.</summary>
    public string? ReportDirectoryPath
    {
        get => _reportDirectoryPath;
        private set
        {
            if (SetProperty(ref _reportDirectoryPath, value))
            {
                OnPropertyChanged(nameof(CanOpenReport));
                OpenReportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanOpenReport => HasFinalResult && ReportDirectoryPath is not null;

    /// <summary>
    /// Attempts a silent cache-backed restore when the window loads. Interactive sign-in
    /// is requested only when no validated session exists.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsBusy || IsSignedIn)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var identity = await _authenticationService.GetCurrentOperatorAsync(CancellationToken.None)
                .ConfigureAwait(true);
            if (identity is not null)
            {
                ApplySignedIn(identity);
            }
        }
        catch (UserFacingAuthException exception)
        {
            ApplyError(exception);
        }
        catch (Exception exception)
        {
            _logger.LogError("Unexpected startup authentication failure: {Message}",
                AuthErrorSanitizer.RedactSensitiveText(exception.Message));
            ApplyError(AuthenticationErrors.Unexpected());
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Releases the exclusive destination lock and cancels any running operation.</summary>
    public void Dispose()
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = null;
        DisposeSession();
    }

    private async Task SignInAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            var handle = WindowHandleProvider?.Invoke() ?? IntPtr.Zero;
            var identity = await _authenticationService
                .SignInAsync(handle, RememberSignIn, CancellationToken.None)
                .ConfigureAwait(true);
            ApplySignedIn(identity);
        }
        catch (UserFacingAuthException exception)
        {
            ApplyError(exception);
        }
        catch (OperationCanceledException)
        {
            ApplyError(AuthenticationErrors.Cancelled());
        }
        catch (Exception exception)
        {
            _logger.LogError("Unexpected sign-in failure: {Message}",
                AuthErrorSanitizer.RedactSensitiveText(exception.Message));
            ApplyError(AuthenticationErrors.Unexpected());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignOutAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            await _authenticationService.SignOutAsync(CancellationToken.None).ConfigureAwait(true);
            IsSignedIn = false;
            OperatorDisplayName = null;
            OperatorUpn = null;
            _operatorIdentity = null;
            InvalidateScan("Signed out.");
            StatusMessage = AuthenticationErrors.SignOutDescription;
        }
        catch (Exception exception)
        {
            _logger.LogError("Sign-out failure: {Message}",
                AuthErrorSanitizer.RedactSensitiveText(exception.Message));
            ApplyError(AuthenticationErrors.Unexpected());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BrowseDestination()
    {
        ClearError();
        try
        {
            var path = _folderPickerService.PickFolder();
            if (!string.IsNullOrWhiteSpace(path))
            {
                DestinationPath = path; // the setter invalidates any previous scan
            }
        }
        catch (Exception exception)
        {
            ApplyException(exception);
        }
    }

    /// <summary>
    /// The mandatory dry run: source resolution (M3), destination session (M4), then
    /// the M5 scan. The scan never downloads employee content and never claims that
    /// anything was copied.
    /// </summary>
    private async Task ScanAsync()
    {
        IsScanning = true;
        ClearError();
        ResetScanState();
        HasFinalResult = false;
        ReportDirectoryPath = null;
        CurrentOperation = "Scanning the employee OneDrive";
        CurrentItemName = null;
        IsProgressIndeterminate = true;
        StatusMessage = "Scanning. The dry run does not copy any content.";
        AddActivity("Scan started.");
        using var cts = new CancellationTokenSource();
        _operationCts = cts;
        try
        {
            var operatorIdentity = await _authenticationService
                .GetCurrentOperatorAsync(cts.Token).ConfigureAwait(true);
            if (operatorIdentity is null)
            {
                ApplyError(AuthenticationErrors.SignInRequired());
                return;
            }

            _operatorIdentity = operatorIdentity;

            var source = await _sourceResolver.ResolveAsync(SourceInput.Trim(), cts.Token)
                .ConfigureAwait(true);
            var session = await _destinationSessionService.OpenAsync(
                    DestinationPath.Trim(),
                    new SourceBindingIdentity(
                        source.TenantId, source.DriveId, source.UserObjectId, source.UserPrincipalName),
                    new Destination.OperatorIdentity(
                        operatorIdentity.EntraObjectId, operatorIdentity.UserPrincipalName),
                    cts.Token)
                .ConfigureAwait(true);
            var result = await _scanService.ScanAsync(source, session, cts.Token).ConfigureAwait(true);

            _resolvedSource = source;
            _session = session;
            _scanIsCurrent = true;
            ApplyScanSummary(source, session, result, operatorIdentity);
            StatusMessage = ScanCompleteStatus;
            AddActivity("Scan completed. Review the summary before copying.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = ScanCancelledStatus;
            AddActivity("Scan cancelled.");
        }
        catch (ScanException exception) when (exception.ReferenceCode == ScanErrorCodes.Cancelled)
        {
            StatusMessage = ScanCancelledStatus;
            AddActivity("Scan cancelled.");
        }
        catch (Exception exception)
        {
            ApplyException(exception);
            StatusMessage = "The scan did not complete. Review the error and try again.";
        }
        finally
        {
            _operationCts = null;
            IsScanning = false;
            if (!HasScanSummary)
            {
                CurrentOperation = null;
            }
        }
    }

    /// <summary>
    /// Starts the copy only after a successful current scan and the explicit operator
    /// confirmation. Scan currency is revalidated immediately before scheduling; the
    /// orchestration enforces the same gate service-side.
    /// </summary>
    private async Task StartCopyAsync()
    {
        if (_resolvedSource is null || _session is null || !_scanIsCurrent || !IsScanConfirmed)
        {
            return;
        }

        IsCopying = true;
        ClearError();
        HasFinalResult = false;
        ReportDirectoryPath = null;
        DiscoveredCount = 0;
        CompletedCount = 0;
        SkippedCount = 0;
        UnsupportedCount = 0;
        FailedCount = 0;
        DownloadedBytes = 0;
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        CurrentOperation = "Starting the copy";
        CurrentItemName = null;
        StatusMessage = "Copy in progress.";
        AddActivity("Copy started.");
        using var cts = new CancellationTokenSource();
        _operationCts = cts;
        var session = _session;
        try
        {
            if (!await _scanService.IsScanCurrentAsync(_resolvedSource, session, cts.Token)
                    .ConfigureAwait(true))
            {
                ApplyException(TransferErrors.ScanNotCurrent());
                InvalidateScan("The scan is no longer current. Run the scan again to enable copying.");
                return;
            }

            var progress = new Progress<TransferProgress>(ApplyProgress);
            var result = await _transferOrchestrator
                .RunAsync(_resolvedSource, session, cts.Token, progress)
                .ConfigureAwait(true);
            ApplyTerminalResult(result, session);
        }
        catch (OperationCanceledException)
        {
            // The orchestration normally returns the Cancelled result itself; this is
            // the defensive path. Completed files and safe partials are preserved.
            SetTerminalState(TransferRunState.Cancelled, runId: null, session);
        }
        catch (Exception exception)
        {
            ApplyException(exception);
            StatusMessage = "The copy did not finish. Review the error and the run report.";
        }
        finally
        {
            _operationCts = null;
            IsCopying = false;
            // The run is over: release the exclusive destination lock and require a
            // fresh scan before another copy can start.
            DisposeSession();
            _scanIsCurrent = false;
            RefreshCommands();
        }
    }

    private void CancelOperation()
    {
        if (_operationCts is null)
        {
            return;
        }

        StatusMessage = IsCopying
            ? "Cancelling the copy. Completed files and safe partial files are preserved."
            : "Cancelling the scan.";
        AddActivity("Cancellation requested.");
        _operationCts.Cancel();
    }

    private void OpenReport()
    {
        if (ReportDirectoryPath is null)
        {
            return;
        }

        ClearError();
        try
        {
            _shellService.OpenFolder(ReportDirectoryPath);
        }
        catch (Exception exception)
        {
            ApplyException(exception);
        }
    }

    private void OpenDestination()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath))
        {
            return;
        }

        ClearError();
        try
        {
            _shellService.OpenFolder(DestinationPath.Trim());
        }
        catch (Exception exception)
        {
            ApplyException(exception);
        }
    }

    private void ApplySignedIn(OperatorIdentity identity)
    {
        _operatorIdentity = identity;
        IsSignedIn = true;
        OperatorDisplayName = identity.DisplayName;
        OperatorUpn = identity.UserPrincipalName;
        StatusMessage = "Signed in and validated as an authorized operator.";
        ClearError();
    }

    private void ApplyScanSummary(
        ResolvedEmployeeSource source,
        DestinationSession session,
        ScanResult result,
        OperatorIdentity operatorIdentity)
    {
        ScanEmployeeDisplay = source.UserPrincipalName is { Length: > 0 } upn
            ? $"{source.DisplayName} ({upn})"
            : source.DisplayName;
        ScanOperatorDisplay = $"{operatorIdentity.DisplayName} ({operatorIdentity.UserPrincipalName})";
        ScanSourceModeDisplay = source.Mode == EmployeeSourceMode.Upn ? "Employee UPN" : "OneDrive root URL";
        ScanDestinationDisplay = session.Destination.RootPath;
        ScanFileCountText = result.FileCount.ToString("N0", CultureInfo.InvariantCulture);
        ScanKnownSizeText = FormatBytes(result.KnownSourceBytes);
        ScanUnsupportedCountText = result.UnsupportedCount.ToString("N0", CultureInfo.InvariantCulture);
        ScanPathWarningCountText = result.Warnings
            .Count(warning => warning.Kind is ScanWarningKind.PathAdjusted
                or ScanWarningKind.PathFailure or ScanWarningKind.UnresolvedParent)
            .ToString("N0", CultureInfo.InvariantCulture);
        ScanStorageWarningCountText = result.Warnings
            .Count(warning => warning.Kind is ScanWarningKind.InsufficientStorage
                or ScanWarningKind.BroadPermissionExposure)
            .ToString("N0", CultureInfo.InvariantCulture);

        ScanWarnings.Clear();
        foreach (var warning in result.Warnings.Take(MaxActivityEntries))
        {
            ScanWarnings.Add(warning.ItemName is { Length: > 0 } itemName
                ? $"{warning.Message} Item: {itemName}"
                : warning.Message);
        }

        TotalKnownBytes = result.KnownSourceBytes;
        HasScanSummary = true;
    }

    private void ApplyProgress(TransferProgress progress)
    {
        CurrentOperation = progress.Operation;
        CurrentItemName = progress.CurrentItemName;
        DiscoveredCount = progress.DiscoveredCount;
        CompletedCount = progress.CompletedCount;
        SkippedCount = progress.SkippedCount;
        UnsupportedCount = progress.UnsupportedCount;
        FailedCount = progress.FailedCount;
        DownloadedBytes = progress.DownloadedBytes;
        if (progress.TotalKnownBytes is not null)
        {
            TotalKnownBytes = progress.TotalKnownBytes;
        }

        if (progress.ActivityMessage is { Length: > 0 } activity)
        {
            AddActivity(activity);
        }

        UpdateProgressBar();
    }

    private void UpdateProgressBar()
    {
        if (TotalKnownBytes is > 0)
        {
            IsProgressIndeterminate = false;
            ProgressPercent = Math.Clamp((double)DownloadedBytes / TotalKnownBytes.Value * 100.0, 0.0, 100.0);
        }
        else
        {
            IsProgressIndeterminate = IsCopying || IsScanning;
            ProgressPercent = 0;
        }
    }

    private void ApplyTerminalResult(TransferRunResult result, DestinationSession session)
    {
        CompletedCount = result.CompletedCount;
        SkippedCount = result.SkippedCount;
        FailedCount = result.FailedCount;
        UnsupportedCount = result.UnsupportedCount;
        foreach (var warning in result.Warnings.Take(MaxActivityEntries))
        {
            AddActivity("Warning: " + warning.Message);
        }

        SetTerminalState(result.FinalState, result.RunId, session);
    }

    /// <summary>
    /// Shows the exact approved terminal run state. <see cref="TransferRunState.Incomplete" />
    /// always states clearly that the local archive is not complete.
    /// </summary>
    private void SetTerminalState(TransferRunState state, string? runId, DestinationSession? session)
    {
        FinalRunState = state;
        FinalStateName = state.ToString();
        (FinalStateTitle, FinalStateExplanation) = state switch
        {
            TransferRunState.Completed => (
                "Copy completed",
                "All supported content was copied and verified. The local archive is complete."),
            TransferRunState.CompletedWithWarnings => (
                "Copy completed with warnings",
                "All supported content was copied and verified, and non-content warnings were recorded. The local archive is complete; review the run report for the warnings."),
            TransferRunState.Incomplete => (
                "The archive is NOT complete",
                "The copy finished its safe work, but the local archive is NOT complete. Some supported content is missing, failed, or unsupported. Review the run report for details."),
            TransferRunState.Failed => (
                "The copy failed",
                "A required check prevented the copy from continuing safely. The local archive is not complete. Review the error and the run report."),
            TransferRunState.Cancelled => (
                "The copy was cancelled",
                "Cancellation was requested and state was preserved safely. Completed files and safe partial files are kept, and the local archive is not complete until a later run finishes."),
            TransferRunState.Interrupted => (
                "The copy was interrupted",
                "A previous run ended without an orderly finish and may be resumed after validation. The local archive is not complete until a later run finishes."),
            _ => (
                "The copy ended",
                "The run reached an unexpected state. Review the run report."),
        };

        if (runId is { Length: > 0 } && session is not null)
        {
            ReportDirectoryPath = ReportWriter.GetRunReportDirectoryPath(session.Destination.RootPath, runId);
        }

        HasFinalResult = true;
        CurrentOperation = null;
        CurrentItemName = null;
        IsProgressIndeterminate = false;
        StatusMessage = FinalStateTitle;
        AddActivity($"Run finished: {FinalStateName}.");
    }

    /// <summary>
    /// Clears the scan-dependent state: the open destination session (and its exclusive
    /// lock), the resolved source, the summary, and the confirmation. No-op when there
    /// is nothing to invalidate, so typing does not churn the status text.
    /// </summary>
    private void InvalidateScan(string statusMessage)
    {
        if (!HasScanSummary && !_scanIsCurrent && _session is null)
        {
            return;
        }

        ResetScanState();
        StatusMessage = statusMessage;
    }

    private void ResetScanState()
    {
        DisposeSession();
        _resolvedSource = null;
        _scanIsCurrent = false;
        IsScanConfirmed = false;
        HasScanSummary = false;
        ScanEmployeeDisplay = null;
        ScanOperatorDisplay = null;
        ScanSourceModeDisplay = null;
        ScanDestinationDisplay = null;
        ScanFileCountText = null;
        ScanKnownSizeText = null;
        ScanUnsupportedCountText = null;
        ScanPathWarningCountText = null;
        ScanStorageWarningCountText = null;
        ScanWarnings.Clear();
    }

    private void DisposeSession()
    {
        _session?.Dispose();
        _session = null;
    }

    private void AddActivity(string message)
    {
        RecentActivity.Add(message);
        while (RecentActivity.Count > MaxActivityEntries)
        {
            RecentActivity.RemoveAt(0);
        }
    }

    private void ApplyError(UserFacingAuthException exception) =>
        ApplyError(exception.Title, exception.Explanation, exception.CorrectiveAction, exception.ReferenceCode);

    private void ApplyError(string title, string explanation, string correctiveAction, string referenceCode)
    {
        ErrorTitle = title;
        ErrorMessage = explanation;
        ErrorAction = correctiveAction;
        ErrorReferenceCode = referenceCode;
        OnPropertyChanged(nameof(HasError));
    }

    private void ApplyException(Exception exception)
    {
        switch (exception)
        {
            case UserFacingAuthException error:
                ApplyError(error.Title, error.Explanation, error.CorrectiveAction, error.ReferenceCode);
                break;
            case SourceResolutionException error:
                ApplyError(error.Title, error.Explanation, error.CorrectiveAction, error.ReferenceCode);
                break;
            case DestinationException error:
                ApplyError(error.Title, error.Explanation, error.CorrectiveAction, error.ReferenceCode);
                break;
            case ScanException error:
                ApplyError(error.Title, error.Explanation, error.CorrectiveAction, error.ReferenceCode);
                break;
            case TransferException error:
                ApplyError(error.Title, error.Explanation, error.CorrectiveAction, error.ReferenceCode);
                break;
            case UserInterfaceException error:
                ApplyError(error.Title, error.Explanation, error.CorrectiveAction, error.ReferenceCode);
                break;
            default:
                // Unexpected failures are wrapped in the generic reference-coded error;
                // raw exception text never reaches the UI.
                _logger.LogError("Unexpected UI failure: {Message}",
                    AuthErrorSanitizer.RedactSensitiveText(exception.Message));
                var fallback = UserInterfaceErrors.Unexpected();
                ApplyError(fallback.Title, fallback.Explanation, fallback.CorrectiveAction, fallback.ReferenceCode);
                break;
        }
    }

    private void ClearError()
    {
        ErrorTitle = null;
        ErrorMessage = null;
        ErrorAction = null;
        ErrorReferenceCode = null;
        OnPropertyChanged(nameof(HasError));
    }

    private void OnOperationStateChanged()
    {
        OnPropertyChanged(nameof(IsOperationRunning));
        OnPropertyChanged(nameof(ShowProgressArea));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        SignInCommand.RaiseCanExecuteChanged();
        SignOutCommand.RaiseCanExecuteChanged();
        BrowseDestinationCommand.RaiseCanExecuteChanged();
        ScanCommand.RaiseCanExecuteChanged();
        StartCopyCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        OpenReportCommand.RaiseCanExecuteChanged();
        OpenDestinationCommand.RaiseCanExecuteChanged();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? bytes.ToString("N0", CultureInfo.InvariantCulture) + " bytes"
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {units[unit]}");
    }
}
