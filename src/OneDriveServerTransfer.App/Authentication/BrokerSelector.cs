namespace OneDriveServerTransfer.Authentication;

/// <summary>
/// Chooses the interactive sign-in surface. WAM is preferred on supported Windows
/// environments; the MSAL system browser is the fallback when WAM is unavailable or
/// unsupported. Embedded web views are never selected.
/// </summary>
public interface IBrokerSelector
{
    InteractiveMode SelectInteractiveMode(bool brokerAvailable);
}

public sealed class WamPreferredBrokerSelector : IBrokerSelector
{
    public InteractiveMode SelectInteractiveMode(bool brokerAvailable) =>
        brokerAvailable ? InteractiveMode.Broker : InteractiveMode.SystemBrowser;
}
