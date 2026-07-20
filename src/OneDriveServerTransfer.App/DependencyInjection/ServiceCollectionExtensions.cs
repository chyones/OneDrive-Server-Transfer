using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Configuration;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.ViewModels;
using Serilog;

namespace OneDriveServerTransfer.DependencyInjection;

/// <summary>
/// Composition root for application services. M1 foundations and the real M2
/// authentication services are registered here. Later-phase abstractions (Microsoft
/// Graph metadata, temporary downloads, retry, hashing, local storage, transfer state,
/// and reports) remain intentionally unregistered: no production implementation exists
/// yet, and no fake service may take their place.
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

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ITransferStateSchemaInitializer, SqliteTransferStateSchemaInitializer>();

        return services;
    }
}
