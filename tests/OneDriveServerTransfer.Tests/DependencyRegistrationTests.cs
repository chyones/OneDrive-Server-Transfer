using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Configuration;
using OneDriveServerTransfer.DependencyInjection;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.ViewModels;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Verifies the dependency-injection composition root: M1 through M5 slice-1 services
/// resolve, and no later-phase abstraction has a registered implementation (fake
/// production services are prohibited).
/// </summary>
public class DependencyRegistrationTests
{
    private static readonly (string Key, string Value)[] ValidAuthConfiguration =
    [
        ("Authentication:TenantId", "11111111-1111-1111-1111-111111111111"),
        ("Authentication:ClientId", "22222222-2222-2222-2222-222222222222"),
        ("Authentication:RedirectUri", "http://localhost"),
        ("SourceResolution:TenantOneDriveHost", "contoso-my.sharepoint.com"),
    ];

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ValidAuthConfiguration.Select(pair =>
                new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ResolvesFoundationServices()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IOptions<TransferStateOptions>>());
        Assert.NotNull(provider.GetRequiredService<ILoggerFactory>());
        Assert.NotNull(provider.GetRequiredService<ILogger<MainViewModel>>());
        Assert.NotNull(provider.GetRequiredService<MainViewModel>());
        Assert.IsType<SqliteTransferStateSchemaInitializer>(
            provider.GetRequiredService<ITransferStateSchemaInitializer>());
    }

    [Fact]
    public void ResolvesRealM2AuthenticationServices()
    {
        using var provider = BuildProvider();

        Assert.IsType<MsalAuthenticationService>(provider.GetRequiredService<IAuthenticationService>());
        Assert.IsType<MsalIdentityClient>(provider.GetRequiredService<IIdentityClient>());
        Assert.IsType<OperatorValidator>(provider.GetRequiredService<IOperatorValidator>());
        Assert.IsType<WamPreferredBrokerSelector>(provider.GetRequiredService<IBrokerSelector>());
        Assert.IsType<DpapiTokenCacheProtector>(provider.GetRequiredService<ITokenCacheProtector>());
        Assert.NotNull(provider.GetRequiredService<ITokenCacheStore>());
        Assert.NotNull(provider.GetRequiredService<PersistentTokenCacheBinder>());
        Assert.NotNull(provider.GetRequiredService<IOperatorProfileProvider>());
    }

    [Fact]
    public void ResolvesRealM3SourceResolutionServices()
    {
        using var provider = BuildProvider();

        Assert.IsType<GraphRetryCoordinator>(provider.GetRequiredService<IRetryCoordinator>());
        Assert.IsType<GraphRequestChannel>(provider.GetRequiredService<IGraphRequestChannel>());
        Assert.IsType<EmployeeSourceResolver>(provider.GetRequiredService<IEmployeeSourceResolver>());
    }

    [Fact]
    public void ResolvesRealM4DestinationServices()
    {
        using var provider = BuildProvider();

        Assert.IsType<DestinationValidator>(provider.GetRequiredService<IDestinationValidator>());
        Assert.IsType<DestinationLayoutService>(provider.GetRequiredService<IDestinationLayoutService>());
        Assert.IsType<DestinationLockService>(provider.GetRequiredService<IDestinationLockService>());
        Assert.IsType<SqliteDestinationBindingStore>(provider.GetRequiredService<IDestinationBindingStore>());
        Assert.IsType<DestinationBindingService>(provider.GetRequiredService<IDestinationBindingService>());
        Assert.IsType<DestinationSessionService>(provider.GetRequiredService<IDestinationSessionService>());
        Assert.IsType<SqlitePathCollisionRegistry>(provider.GetRequiredService<IPathCollisionRegistry>());
        Assert.Same(
            provider.GetRequiredService<SqlitePathCollisionRegistry>(),
            provider.GetRequiredService<IPathCollisionRegistry>());
        Assert.IsType<PathMapperV1>(provider.GetRequiredService<IPathMapper>());
        Assert.IsType<DestinationPathGuard>(provider.GetRequiredService<IDestinationPathGuard>());
        Assert.IsType<DestinationCapacityService>(provider.GetRequiredService<IDestinationCapacityService>());
        Assert.IsType<DestinationSecurityEvaluator>(provider.GetRequiredService<IDestinationSecurityEvaluator>());
        Assert.IsType<LocalStorageService>(provider.GetRequiredService<ILocalStorageService>());
    }

    [Fact]
    public void ResolvesRealM5ScanInventoryAndStateServices()
    {
        using var provider = BuildProvider();

        Assert.IsType<DeltaInventoryClient>(provider.GetRequiredService<IDeltaInventoryClient>());
        Assert.IsType<SqliteTransferStateStore>(provider.GetRequiredService<ITransferStateStore>());
        Assert.IsType<ScanService>(provider.GetRequiredService<IScanService>());
    }

    public static TheoryData<Type> LaterPhaseInterfaces => new()
    {
        typeof(IGraphMetadataClient),
        typeof(ITemporaryDownloadClient),
        typeof(IHashingService),
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
