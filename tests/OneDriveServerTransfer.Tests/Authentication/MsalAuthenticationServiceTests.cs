using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Tests.TestSupport;

namespace OneDriveServerTransfer.Tests.Authentication;

public class MsalAuthenticationServiceTests : IDisposable
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string OtherTenantId = "44444444-4444-4444-4444-444444444444";
    private const string ObjectId = "33333333-3333-3333-3333-333333333333";
    private const string Upn = "operator@example.test";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"odst-auth-{Guid.NewGuid():N}");

    private string CachePath => Path.Combine(_directory, "msal-token-cache.bin");

    /// <summary>
    /// Options wrapper that runs the real AuthenticationOptionsValidator on Value
    /// access, mirroring the production options pipeline (Options.Create skips
    /// validation).
    /// </summary>
    private sealed class ValidatingOptions(AuthenticationOptions options) : IOptions<AuthenticationOptions>
    {
        public AuthenticationOptions Value
        {
            get
            {
                var result = new AuthenticationOptionsValidator().Validate(null, options);
                if (result.Failed)
                {
                    throw new OptionsValidationException(
                        nameof(AuthenticationOptions), typeof(AuthenticationOptions), result.Failures ?? []);
                }

                return options;
            }
        }
    }

    private sealed class Harness
    {
        public required FakeIdentityClient IdentityClient { get; init; }
        public required FakeOperatorProfileProvider ProfileProvider { get; init; }
        public required AuthenticationOptions Options { get; init; }
        public required PersistentTokenCacheBinder Binder { get; init; }
        public required MsalAuthenticationService Service { get; init; }
    }

    private Harness CreateHarness(ITokenCacheProtector? protector = null, Action<AuthenticationOptions>? configure = null)
    {
        var options = new AuthenticationOptions
        {
            TenantId = TenantId,
            ClientId = "22222222-2222-2222-2222-222222222222",
            RedirectUri = "http://localhost",
        };
        configure?.Invoke(options);

        var identityClient = new FakeIdentityClient();
        var profileProvider = new FakeOperatorProfileProvider
        {
            Profile = new OperatorProfile(ObjectId, Upn, "Test Operator"),
        };
        var binder = new PersistentTokenCacheBinder(
            new TokenCacheFileStore(CachePath),
            protector ?? new ReversingTokenCacheProtector(),
            NullLogger<PersistentTokenCacheBinder>.Instance);

        var service = new MsalAuthenticationService(
            identityClient,
            profileProvider,
            new OperatorValidator(new ValidatingOptions(options)),
            new WamPreferredBrokerSelector(),
            binder,
            new ValidatingOptions(options),
            NullLogger<MsalAuthenticationService>.Instance);

        return new Harness
        {
            IdentityClient = identityClient,
            ProfileProvider = profileProvider,
            Options = options,
            Binder = binder,
            Service = service,
        };
    }

    private static IdentityTokenResult ValidToken() => FakeIdentityClient.TokenFor(TenantId, ObjectId, Upn);

    [Fact]
    public async Task SignInWithBrokerAvailableValidatesOperator()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());

        var identity = await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);

        Assert.Equal(ObjectId, identity.EntraObjectId);
        Assert.Equal(Upn, identity.UserPrincipalName);
        Assert.Equal("Test Operator", identity.DisplayName);
        Assert.Equal(TenantId, identity.TenantId);
        Assert.Equal(AuthenticationState.SignedInValidated, harness.Service.State);
        Assert.True(harness.Service.IsSignedIn);
        Assert.Equal([InteractiveMode.Broker], harness.IdentityClient.RequestedInteractiveModes);
    }

    [Fact]
    public async Task SignInFallsBackToSystemBrowserWhenBrokerUnavailable()
    {
        var harness = CreateHarness();
        harness.IdentityClient.BrokerAvailable = false;
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());

        await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);

        Assert.Equal([InteractiveMode.SystemBrowser], harness.IdentityClient.RequestedInteractiveModes);
        Assert.Equal(AuthenticationState.SignedInValidated, harness.Service.State);
    }

    [Fact]
    public async Task SignInRetriesOnceWithSystemBrowserWhenBrokerFailsAtRuntime()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, mode) =>
            mode == InteractiveMode.Broker
                ? Task.FromException<IdentityTokenResult>(new MsalClientException("broker_unavailable", "hidden"))
                : Task.FromResult(ValidToken());

        var identity = await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Equal(
            [InteractiveMode.Broker, InteractiveMode.SystemBrowser],
            harness.IdentityClient.RequestedInteractiveModes);
    }

    [Fact]
    public async Task SignInCancellationReturnsReferenceCodedError()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) =>
            Task.FromException<IdentityTokenResult>(
                new MsalClientException(MsalError.AuthenticationCanceledError, "hidden"));

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.Cancelled, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.SignedOut, harness.Service.State);
        Assert.False(harness.Service.IsSignedIn);
    }

    [Fact]
    public async Task SignInConsentFailureIsClassified()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) =>
            Task.FromException<IdentityTokenResult>(new MsalServiceException("invalid_grant", "AADSTS65001: hidden"));

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.ConsentRequired, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.AuthenticationFailed, harness.Service.State);
    }

    [Fact]
    public async Task SignInRejectsTokenFromAnotherTenant()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) =>
            Task.FromResult(FakeIdentityClient.TokenFor(OtherTenantId, ObjectId, Upn));

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.TenantMismatch, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.SignedInUnauthorized, harness.Service.State);
        Assert.NotEmpty(harness.IdentityClient.RemovedAccounts);
    }

    [Fact]
    public async Task SignInRejectsGuestAccount()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) =>
            Task.FromResult(FakeIdentityClient.TokenFor(TenantId, ObjectId, Upn, homeTenantId: OtherTenantId));

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.GuestAccountRejected, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.SignedInUnauthorized, harness.Service.State);
    }

    [Fact]
    public async Task SignInRejectsOperatorOutsideAllowlist()
    {
        var harness = CreateHarness(configure: options =>
            options.AuthorizedOperatorObjectIds = ["55555555-5555-5555-5555-555555555555"]);
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.OperatorNotAuthorized, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.SignedInUnauthorized, harness.Service.State);
        Assert.NotEmpty(harness.IdentityClient.RemovedAccounts);
    }

    [Fact]
    public async Task SignInAcceptsAllowlistedOperator()
    {
        var harness = CreateHarness(configure: options =>
            options.AuthorizedOperatorObjectIds = [ObjectId]);
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());

        var identity = await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);

        Assert.Equal(ObjectId, identity.EntraObjectId);
    }

    [Fact]
    public async Task SignInProfileUnauthorizedIsClassified()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());
        harness.ProfileProvider.Failure = new OperatorProfileException(OperatorProfileFailure.Unauthorized, 401, "hidden");

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.SessionUnauthorized, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.AuthenticationFailed, harness.Service.State);
    }

    [Fact]
    public async Task SignInProfileForbiddenIsClassified()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());
        harness.ProfileProvider.Failure = new OperatorProfileException(OperatorProfileFailure.Forbidden, 403, "hidden");

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.AccessForbidden, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.AuthenticationFailed, harness.Service.State);
    }

    [Fact]
    public async Task SignInRejectsProfileTokenIdentityMismatch()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());
        harness.ProfileProvider.Profile = new OperatorProfile(
            "99999999-9999-9999-9999-999999999999", Upn, "Other Operator");

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.IdentityMismatch, exception.ReferenceCode);
    }

    [Fact]
    public async Task RememberSignInDisabledClearsPersistedCacheAndSkipsWrites()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(CachePath, "stale-cache"u8.ToArray());
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());

        await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: false, CancellationToken.None);

        Assert.False(File.Exists(CachePath));
        Assert.False(harness.Binder.PersistenceEnabled);
    }

    [Fact]
    public async Task RememberSignInEnabledKeepsPersistenceActive()
    {
        var harness = CreateHarness();
        harness.Binder.PersistenceEnabled = false;
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());

        await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);

        Assert.True(harness.Binder.PersistenceEnabled);
    }

    [Fact]
    public async Task SilentRestoreReturnsValidatedOperator()
    {
        var harness = CreateHarness();
        harness.IdentityClient.Accounts.Add(FakeIdentityClient.AccountFor(TenantId, ObjectId, Upn));
        harness.IdentityClient.SilentHandler = (_, _) => Task.FromResult(ValidToken());

        var identity = await harness.Service.GetCurrentOperatorAsync(CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Equal(ObjectId, identity.EntraObjectId);
        Assert.Equal(AuthenticationState.SignedInValidated, harness.Service.State);
    }

    [Fact]
    public async Task NoCachedAccountMeansInteractiveSignInRequired()
    {
        var harness = CreateHarness();

        var identity = await harness.Service.GetCurrentOperatorAsync(CancellationToken.None);

        Assert.Null(identity);
        Assert.Equal(AuthenticationState.InteractiveSignInRequired, harness.Service.State);
    }

    [Fact]
    public async Task SilentFailureMeansInteractiveSignInRequired()
    {
        var harness = CreateHarness();
        harness.IdentityClient.Accounts.Add(FakeIdentityClient.AccountFor(TenantId, ObjectId, Upn));
        harness.IdentityClient.SilentHandler = (_, _) =>
            Task.FromException<IdentityTokenResult>(new MsalUiRequiredException("invalid_grant", "hidden"));

        var identity = await harness.Service.GetCurrentOperatorAsync(CancellationToken.None);

        Assert.Null(identity);
        Assert.Equal(AuthenticationState.InteractiveSignInRequired, harness.Service.State);
    }

    [Fact]
    public async Task CacheCorruptionFailsSafelyAndRequiresReauthentication()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(CachePath, "damaged"u8.ToArray());
        var harness = CreateHarness(new CorruptingTokenCacheProtector());

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.GetCurrentOperatorAsync(CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.CacheCorrupted, exception.ReferenceCode);
        Assert.False(File.Exists(CachePath));
        Assert.Equal(AuthenticationState.InteractiveSignInRequired, harness.Service.State);

        // Reset safety: the next attempt proceeds normally with a cleared cache.
        var identity = await harness.Service.GetCurrentOperatorAsync(CancellationToken.None);
        Assert.Null(identity);
    }

    [Fact]
    public async Task AcquireGraphAccessTokenReturnsSilentToken()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());
        harness.IdentityClient.SilentHandler = (_, _) => Task.FromResult(ValidToken());

        await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);
        var token = await harness.Service.AcquireGraphAccessTokenAsync(CancellationToken.None);

        Assert.Equal("test-access-token", token);
    }

    [Fact]
    public async Task AcquireGraphAccessTokenWithoutSignInFails()
    {
        var harness = CreateHarness();

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.AcquireGraphAccessTokenAsync(CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.SignInRequired, exception.ReferenceCode);
    }

    [Fact]
    public async Task AcquireGraphAccessTokenSilentExpiryRequiresReauthentication()
    {
        var harness = CreateHarness();
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());
        await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);

        harness.IdentityClient.SilentHandler = (_, _) =>
            Task.FromException<IdentityTokenResult>(new MsalUiRequiredException("invalid_grant", "hidden"));

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.AcquireGraphAccessTokenAsync(CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.ReauthenticationRequired, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.ReauthenticationRequired, harness.Service.State);
    }

    [Fact]
    public async Task SignOutRemovesAccountsAndPersistentCache()
    {
        var harness = CreateHarness();
        var account = FakeIdentityClient.AccountFor(TenantId, ObjectId, Upn);
        harness.IdentityClient.Accounts.Add(account);
        harness.IdentityClient.InteractiveHandler = (_, _) => Task.FromResult(ValidToken());
        harness.IdentityClient.SilentHandler = (_, _) => Task.FromResult(ValidToken());

        await harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None);
        await harness.Service.SignOutAsync(CancellationToken.None);

        Assert.Equal(AuthenticationState.SignedOut, harness.Service.State);
        Assert.False(harness.Service.IsSignedIn);
        Assert.Contains(harness.IdentityClient.RemovedAccounts, removed => removed.HomeAccountId == account.HomeAccountId);
        Assert.False(File.Exists(CachePath));
    }

    [Fact]
    public async Task InvalidConfigurationFailsSafely()
    {
        var harness = CreateHarness(configure: options => options.TenantId = "CONFIGURE_TENANT_ID");

        var exception = await Assert.ThrowsAsync<UserFacingAuthException>(
            () => harness.Service.SignInAsync(IntPtr.Zero, rememberSignIn: true, CancellationToken.None));

        Assert.Equal(AuthenticationErrorCodes.InvalidConfiguration, exception.ReferenceCode);
        Assert.Equal(AuthenticationState.AuthenticationFailed, harness.Service.State);
    }

    [Fact]
    public void SignOutWordingDoesNotOverclaim()
    {
        Assert.Contains("application", AuthenticationErrors.SignOutDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not affected", AuthenticationErrors.SignOutDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("all sessions", AuthenticationErrors.SignOutDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signed out of Windows", AuthenticationErrors.SignOutDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signed out of your browser", AuthenticationErrors.SignOutDescription, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp directory.
        }
    }
}
