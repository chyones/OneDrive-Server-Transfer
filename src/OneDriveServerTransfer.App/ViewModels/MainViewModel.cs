using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.ViewModels;

/// <summary>
/// View model for the single application window. M2 scope: operator sign-in, signed-in
/// operator display, remember sign-in, and sign-out. The transfer workflow (employee
/// source, destination, Scan, Start Copy, progress, reports) arrives in later
/// milestones and intentionally does not exist here.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<MainViewModel> _logger;

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

    public MainViewModel(
        IAuthenticationService authenticationService,
        IOptions<AuthenticationOptions> options,
        ILogger<MainViewModel> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
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

        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !IsBusy && !IsSignedIn);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync, () => !IsBusy && IsSignedIn);
    }

    /// <summary>Provides the owner window handle for the interactive sign-in flow.</summary>
    public Func<IntPtr>? WindowHandleProvider { get; set; }

    public string WindowTitle => "OneDrive Server Transfer";

    public AsyncRelayCommand SignInCommand { get; }

    public AsyncRelayCommand SignOutCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsSignedIn
    {
        get => _isSignedIn;
        private set => SetProperty(ref _isSignedIn, value);
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
        NotifyCommandStates();
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
            NotifyCommandStates();
        }
    }

    private async Task SignInAsync()
    {
        IsBusy = true;
        ClearError();
        NotifyCommandStates();
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
            NotifyCommandStates();
        }
    }

    private async Task SignOutAsync()
    {
        IsBusy = true;
        ClearError();
        NotifyCommandStates();
        try
        {
            await _authenticationService.SignOutAsync(CancellationToken.None).ConfigureAwait(true);
            IsSignedIn = false;
            OperatorDisplayName = null;
            OperatorUpn = null;
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
            NotifyCommandStates();
        }
    }

    private void ApplySignedIn(OperatorIdentity identity)
    {
        IsSignedIn = true;
        OperatorDisplayName = identity.DisplayName;
        OperatorUpn = identity.UserPrincipalName;
        StatusMessage = "Signed in and validated as an authorized operator.";
        ClearError();
    }

    private void ApplyError(UserFacingAuthException exception)
    {
        ErrorTitle = exception.Title;
        ErrorMessage = exception.Explanation;
        ErrorAction = exception.CorrectiveAction;
        ErrorReferenceCode = exception.ReferenceCode;
        OnPropertyChanged(nameof(HasError));
        StatusMessage = exception.Title;
    }

    private void ClearError()
    {
        ErrorTitle = null;
        ErrorMessage = null;
        ErrorAction = null;
        ErrorReferenceCode = null;
        OnPropertyChanged(nameof(HasError));
    }

    private void NotifyCommandStates()
    {
        SignInCommand.RaiseCanExecuteChanged();
        SignOutCommand.RaiseCanExecuteChanged();
    }
}
