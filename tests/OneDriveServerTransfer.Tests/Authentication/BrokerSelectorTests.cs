using OneDriveServerTransfer.Authentication;

namespace OneDriveServerTransfer.Tests.Authentication;

public class BrokerSelectorTests
{
    private readonly WamPreferredBrokerSelector _selector = new();

    [Fact]
    public void BrokerIsPreferredWhenAvailable()
    {
        Assert.Equal(InteractiveMode.Broker, _selector.SelectInteractiveMode(brokerAvailable: true));
    }

    [Fact]
    public void SystemBrowserIsSelectedWhenBrokerUnavailable()
    {
        Assert.Equal(InteractiveMode.SystemBrowser, _selector.SelectInteractiveMode(brokerAvailable: false));
    }
}
