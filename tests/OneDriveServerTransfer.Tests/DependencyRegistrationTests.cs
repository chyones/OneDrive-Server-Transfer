using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Configuration;
using OneDriveServerTransfer.DependencyInjection;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Verifies the dependency-injection composition root: M1 services resolve, and no
/// later-phase abstraction has a registered implementation (fake production services
/// are prohibited).
/// </summary>
public class DependencyRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ResolvesM1FoundationServices()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IOptions<TransferStateOptions>>());
        Assert.NotNull(provider.GetRequiredService<ILoggerFactory>());
        Assert.NotNull(provider.GetRequiredService<ILogger<MainViewModel>>());
        Assert.NotNull(provider.GetRequiredService<MainViewModel>());
        Assert.IsType<SqliteTransferStateSchemaInitializer>(
            provider.GetRequiredService<ITransferStateSchemaInitializer>());
    }

    public static TheoryData<Type> LaterPhaseInterfaces => new()
    {
        typeof(IAuthenticationService),
        typeof(IGraphMetadataClient),
        typeof(ITemporaryDownloadClient),
        typeof(IRetryCoordinator),
        typeof(IHashingService),
        typeof(ILocalStorageService),
        typeof(ITransferStateStore),
        typeof(IReportWriter),
    };

    [Theory]
    [MemberData(nameof(LaterPhaseInterfaces))]
    public void DoesNotRegisterLaterPhaseServices(Type serviceType)
    {
        using var provider = BuildProvider();

        Assert.Null(provider.GetService(serviceType));
    }
}
