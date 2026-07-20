using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using OneDriveServerTransfer.Abstractions;

namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Orchestrates delegated interactive sign-in for the authorized IT transfer operator:
/// WAM-preferred interactive acquisition with system-browser fallback, silent renewal,
/// tenant and allowlist validation, DPAPI-persistent cache control, and truthful
/// sign-out. All failures surface as <see cref="UserFacingAuthException" /> with stable
/// reference codes; protected logs carry only approved audit fields.
/// </summary>
public sealed class MsalAuthenticationService : IAuthenticationService
{
    private readonly IIdentityClient _identityClient;
    private readonly IOperatorProfileProvider _profileProvider;
    private readonly IOperatorValidator _validator;
    private readonly IBrokerSelector _brokerSelector;
    private readonly PersistentTokenCacheBinder _cacheBinder;
    private readonly IOptions<AuthenticationOptions> _options;
    private readonly ILogger<MsalAuthenticationService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private OperatorIdentity? _currentOperator;
    private CachedAccount? _currentAccount;

    public MsalAuthenticationService(
        IIdentityClient identityClient,
        IOperatorProfileProvider profileProvider,
        IOperatorValidator validator,
        IBrokerSelector brokerSelector,
        PersistentTokenCacheBinder cacheBinder,
        IOptions<AuthenticationOptions> options,
        ILogger<MsalAuthenticationService> logger)
    {
        _identityClient = identityClient ?? throw new ArgumentNullException(nameof(identityClient));
        _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _brokerSelector = brokerSelector ?? throw new ArgumentNullException(nameof(brokerSelector));
        _cacheBinder = cacheBinder ?? throw new ArgumentNullException(nameof(cacheBinder));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AuthenticationState State { get; private set; } = AuthenticationState.SignedOut;

    public bool IsSignedIn => State == AuthenticationState.SignedInValidated && _currentOperator is not null;

    public async Task<OperatorIdentity?> GetCurrentOperatorAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentOperator is not null)
            {
                return _currentOperator;
            }

            EnsureConfigured();

            await _cacheBinder.LoadCacheBytesAsync(cancellationToken).ConfigureAwait(false);
            if (_cacheBinder.CorruptionDetected)
            {
                // Fail safely: the damaged cache was cleared without reading its
                // content; reauthentication is required.
                await _cacheBinder.ClearAsync(cancellationToken).ConfigureAwait(false);
                TransitionTo(AuthenticationState.InteractiveSignInRequired);
                _logger.LogWarning("{Summary}", AuthErrorSanitizer.BuildLogSummary(
                    AuthenticationErrorCodes.CacheCorrupted,
                    new SanitizedAuthError(AuthFailureKind.Unknown, null, null, null, null)));
                throw AuthenticationErrors.CacheCorrupted();
            }

            var accounts = await _identityClient.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
            if (accounts.Count == 0)
            {
                TransitionTo(AuthenticationState.InteractiveSignInRequired);
                return null;
            }

            try
            {
                var token = await _identityClient
                    .AcquireTokenSilentAsync(ApprovedScopes.Delegated, accounts[0], cancellationToken)
                    .ConfigureAwait(false);

                var identity = await EstablishOperatorAsync(token, cancellationToken).ConfigureAwait(false);
                TransitionTo(AuthenticationState.SignedInValidated);
                return identity;
            }
            catch (MsalUiRequiredException)
            {
                TransitionTo(AuthenticationState.InteractiveSignInRequired);
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperatorIdentity> SignInAsync(
        IntPtr parentWindowHandle,
        bool rememberSignIn,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConfigured();
            TransitionTo(AuthenticationState.SigningIn);

            // Remember sign-in controls only the application-owned persistent cache.
            _cacheBinder.PersistenceEnabled = rememberSignIn;
            if (!rememberSignIn)
            {
                await _cacheBinder.ClearAsync(cancellationToken).ConfigureAwait(false);
            }

            var token = await AcquireInteractiveAsync(parentWindowHandle, cancellationToken).ConfigureAwait(false);

            var identity = await EstablishOperatorAsync(token, cancellationToken).ConfigureAwait(false);
            TransitionTo(AuthenticationState.SignedInValidated);
            _logger.LogInformation(
                "Operator sign-in validated; tenantMatch={TenantMatch}; allowlistMatch={AllowlistMatch}",
                true, true);
            return identity;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TransitionTo(AuthenticationState.SigningOut);

            var accounts = await _identityClient.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var account in accounts)
            {
                await _identityClient.RemoveAccountAsync(account, cancellationToken).ConfigureAwait(false);
            }

            await _cacheBinder.ClearAsync(cancellationToken).ConfigureAwait(false);
            _currentOperator = null;
            _currentAccount = null;
            TransitionTo(AuthenticationState.SignedOut);
            _logger.LogInformation("Application sign-out completed; persistent cache removed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> AcquireGraphAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConfigured();

            if (_currentAccount is null)
            {
                throw AuthenticationErrors.SignInRequired();
            }

            try
            {
                var token = await _identityClient
                    .AcquireTokenSilentAsync(ApprovedScopes.Delegated, _currentAccount, cancellationToken)
                    .ConfigureAwait(false);

                return token.AccessToken;
            }
            catch (MsalUiRequiredException exception)
            {
                var error = MsalErrorClassifier.Classify(exception);
                TransitionTo(AuthenticationState.ReauthenticationRequired);
                _logger.LogWarning("{Summary}", AuthErrorSanitizer.BuildLogSummary(
                    AuthenticationErrorCodes.ReauthenticationRequired, error));
                throw AuthenticationErrors.ReauthenticationRequired();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IdentityTokenResult> AcquireInteractiveAsync(
        IntPtr parentWindowHandle,
        CancellationToken cancellationToken)
    {
        var brokerAvailable = await _identityClient.IsBrokerAvailableAsync(cancellationToken).ConfigureAwait(false);
        var mode = _brokerSelector.SelectInteractiveMode(brokerAvailable);

        try
        {
            return await _identityClient
                .AcquireTokenInteractiveAsync(ApprovedScopes.Delegated, mode, parentWindowHandle, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MsalClientException exception) when (ShouldRetryWithBrowser(exception, mode))
        {
            _logger.LogInformation("Broker sign-in was unavailable; falling back to the system browser.");
            return await _identityClient
                .AcquireTokenInteractiveAsync(
                    ApprovedScopes.Delegated, InteractiveMode.SystemBrowser, parentWindowHandle, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw MapInteractiveFailure(exception);
        }
    }

    private static bool ShouldRetryWithBrowser(MsalClientException exception, InteractiveMode mode) =>
        mode == InteractiveMode.Broker &&
        MsalErrorClassifier.IsBrokerUnavailable(MsalErrorClassifier.Classify(exception));

    private UserFacingAuthException MapInteractiveFailure(Exception exception)
    {
        var error = MsalErrorClassifier.Classify(exception);

        var (state, failure) = error.Kind switch
        {
            AuthFailureKind.Cancelled => (AuthenticationState.SignedOut, AuthenticationErrors.Cancelled()),
            AuthFailureKind.ConsentRequired => (AuthenticationState.AuthenticationFailed, AuthenticationErrors.ConsentRequired()),
            AuthFailureKind.ServiceUnavailable => (AuthenticationState.AuthenticationFailed, AuthenticationErrors.ServiceUnavailable()),
            _ => (AuthenticationState.AuthenticationFailed, AuthenticationErrors.Unexpected()),
        };

        TransitionTo(state);
        _logger.LogWarning("{Summary}", AuthErrorSanitizer.BuildLogSummary(failure.ReferenceCode, error));
        return failure;
    }

    private async Task<OperatorIdentity> EstablishOperatorAsync(
        IdentityTokenResult token,
        CancellationToken cancellationToken)
    {
        OperatorProfile profile;
        try
        {
            profile = await _profileProvider
                .GetCurrentOperatorProfileAsync(token.AccessToken, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperatorProfileException exception)
        {
            var failure = exception.Failure switch
            {
                OperatorProfileFailure.Unauthorized => AuthenticationErrors.SessionUnauthorized(),
                OperatorProfileFailure.Forbidden => AuthenticationErrors.AccessForbidden(),
                OperatorProfileFailure.InvalidResponse => AuthenticationErrors.IdentityMismatch(),
                _ => AuthenticationErrors.ServiceUnavailable(),
            };

            TransitionTo(AuthenticationState.AuthenticationFailed);
            _logger.LogWarning(
                "Operator profile validation failed; reference={Reference}; httpStatus={HttpStatus}",
                failure.ReferenceCode, exception.HttpStatusCode?.ToString() ?? "n/a");
            throw failure;
        }

        if (!string.Equals(profile.ObjectId, token.ObjectId, StringComparison.OrdinalIgnoreCase))
        {
            TransitionTo(AuthenticationState.AuthenticationFailed);
            _logger.LogWarning("Operator identity mismatch between token subject and profile result.");
            throw AuthenticationErrors.IdentityMismatch();
        }

        var validation = _validator.Validate(new OperatorClaims(
            token.TokenTenantId,
            token.Account.HomeTenantId,
            profile.ObjectId,
            profile.UserPrincipalName,
            token.IdentityProvider,
            token.GrantedScopes));

        if (!validation.IsValid)
        {
            await _identityClient.RemoveAccountAsync(token.Account, cancellationToken).ConfigureAwait(false);
            TransitionTo(AuthenticationState.SignedInUnauthorized);
            _logger.LogWarning("Operator validation failed; reference={Reference}", validation.FailureReferenceCode);

            throw validation.FailureReferenceCode switch
            {
                AuthenticationErrorCodes.TenantMismatch => AuthenticationErrors.TenantMismatch(),
                AuthenticationErrorCodes.GuestAccountRejected => AuthenticationErrors.GuestAccountRejected(),
                AuthenticationErrorCodes.OperatorNotAuthorized => AuthenticationErrors.OperatorNotAuthorized(),
                AuthenticationErrorCodes.RequiredScopeMissing => AuthenticationErrors.RequiredScopeMissing(),
                _ => AuthenticationErrors.IdentityMismatch(),
            };
        }

        _currentAccount = token.Account;
        _currentOperator = new OperatorIdentity(
            profile.ObjectId,
            profile.UserPrincipalName,
            profile.DisplayName,
            _options.Value.TenantId);

        if (_cacheBinder.CorruptionDetected)
        {
            _logger.LogWarning("A damaged application token cache was cleared before sign-in completed.");
        }

        return _currentOperator;
    }

    private void EnsureConfigured()
    {
        try
        {
            _ = _options.Value;
        }
        catch (OptionsValidationException exception)
        {
            TransitionTo(AuthenticationState.AuthenticationFailed);
            _logger.LogError("Authentication configuration is invalid; sign-in cannot proceed.");
            throw AuthenticationErrors.InvalidConfiguration(exception);
        }
    }

    private void TransitionTo(AuthenticationState state)
    {
        State = state;
        _logger.LogDebug("Authentication state changed to {State}", state);
    }
}
