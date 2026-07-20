namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Approved application authentication states from the authentication and token policy.
/// Only <see cref="SignedInValidated" /> may proceed to employee source resolution or
/// scan in later milestones.
/// </summary>
public enum AuthenticationState
{
    SignedOut,
    InteractiveSignInRequired,
    SigningIn,
    SignedInValidated,
    SignedInUnauthorized,
    ReauthenticationRequired,
    SigningOut,
    AuthenticationFailed
}
