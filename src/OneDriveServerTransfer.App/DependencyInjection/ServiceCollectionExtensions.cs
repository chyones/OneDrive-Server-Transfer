using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Configuration;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.ViewModels;
using Serilog;

namespace OneDriveServerTransfer.DependencyInjection;

/// <summary>
/// Composition root for application services. M1 foundations, M2 authentication, M3
/// employee source resolution, and M4 local destination and source binding are
/// registered here. Later-phase abstractions (Microsoft Graph metadata inventory,
/// temporary downloads, hashing, transfer state, and reports) remain intentionally
/// unregistered: no production implementation exists yet, and no fake service may take
/// their place.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<TransferStateOptions>()
            .Bind(configuration.GetSection(TransferStateOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthenticationOptions>, AuthenticationOptionsValidator>();

        services
            .AddOptions<SourceResolutionOptions>()
            .Bind(configuration.GetSection(SourceResolutionOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SourceResolutionOptions>, SourceResolutionOptionsValidator>();

        services.AddLogging();
        services.AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(serviceProvider)
            .Enrich.FromLogContext());

        // M2 authentication services (real implementations; no fakes).
        services.AddSingleton<ITokenCacheProtector, DpapiTokenCacheProtector>();
        services.AddSingleton<ITokenCacheStore>(_ => TokenCacheFileStore.CreateDefault());
        services.AddSingleton<PersistentTokenCacheBinder>();
        services.AddSingleton<IBrokerSelector, WamPreferredBrokerSelector>();
        services.AddSingleton<IOperatorValidator, OperatorValidator>();
        services.AddHttpClient<IOperatorProfileProvider, OperatorProfileProvider>();
        services.AddSingleton<IIdentityClient, MsalIdentityClient>();
        services.AddSingleton<IAuthenticationService, MsalAuthenticationService>();

        // M3 employee source resolution (real implementations; no fakes). The retry
        // coordinator is the single retry owner for Graph metadata requests.
        services.AddHttpClient("graph");
        services.AddSingleton<IRetryCoordinator, GraphRetryCoordinator>();
        services.AddSingleton<IGraphRequestChannel, GraphRequestChannel>();
        services.AddSingleton<IEmployeeSourceResolver, EmployeeSourceResolver>();

        // M4 local destination and source binding (real implementations; no fakes).
        // The collision registry is the in-memory M4 default; M5 substitutes the
        // SQLite-backed registry against the same seam so mappings persist (rule 10).
        services.AddSingleton<IDestinationValidator, DestinationValidator>();
        services.AddSingleton<IDestinationLayoutService, DestinationLayoutService>();
        services.AddSingleton<IDestinationLockService, DestinationLockService>();
        services.AddSingleton<IDestinationBindingStore, SqliteDestinationBindingStore>();
        services.AddSingleton<IDestinationBindingService, DestinationBindingService>();
        services.AddSingleton<IDestinationSessionService, DestinationSessionService>();
        services.AddSingleton<IPathCollisionRegistry, InMemoryPathCollisionRegistry>();
        services.AddSingleton<IPathMapper, PathMapperV1>();
        services.AddSingleton<IDestinationPathGuard, DestinationPathGuard>();
        services.AddSingleton<IDestinationCapacityService, DestinationCapacityService>();
        services.AddSingleton<IDestinationSecurityEvaluator, DestinationSecurityEvaluator>();
        services.AddSingleton<ILocalStorageService, LocalStorageService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ITransferStateSchemaInitializer, SqliteTransferStateSchemaInitializer>();

        return services;
    }
}
