using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Configuration;
using OneDriveServerTransfer.Destination;
using OneDriveServerTransfer.Inventory;
using OneDriveServerTransfer.Reporting;
using OneDriveServerTransfer.Scan;
using OneDriveServerTransfer.SourceResolution;
using OneDriveServerTransfer.State;
using OneDriveServerTransfer.Transfer;
using OneDriveServerTransfer.Verification;
using OneDriveServerTransfer.ViewModels;
using Serilog;

namespace OneDriveServerTransfer.DependencyInjection;

/// <summary>
/// Composition root for application services. M1 foundations, M2 authentication, M3
/// employee source resolution, M4 local destination and source binding, the M5 scan,
/// delta inventory, transfer state, download, verification, transfer engine, and copy
/// orchestration, and the M6 report writer are registered here.
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
        services.AddSingleton<RunReportLogSink>();
        services.AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(serviceProvider)
            .Enrich.FromLogContext()
            // The per-run TransferLog.log sink; it only emits while a run log is open.
            .WriteTo.Sink(serviceProvider.GetRequiredService<RunReportLogSink>()));

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
        // The collision registry is the SQLite-backed M5 implementation, so every
        // mapping persists in the state database and is reused on resume and rerun
        // (rule 10). The concrete type is registered as well because the scan service
        // binds it to the session database before mapping.
        services.AddSingleton<IDestinationValidator, DestinationValidator>();
        services.AddSingleton<IDestinationLayoutService, DestinationLayoutService>();
        services.AddSingleton<IDestinationLockService, DestinationLockService>();
        services.AddSingleton<IDestinationBindingStore, SqliteDestinationBindingStore>();
        services.AddSingleton<IDestinationBindingService, DestinationBindingService>();
        services.AddSingleton<IDestinationSessionService, DestinationSessionService>();
        services.AddSingleton<SqlitePathCollisionRegistry>();
        services.AddSingleton<IPathCollisionRegistry>(
            provider => provider.GetRequiredService<SqlitePathCollisionRegistry>());
        services.AddSingleton<IPathMapper, PathMapperV1>();
        services.AddSingleton<IDestinationPathGuard, DestinationPathGuard>();
        services.AddSingleton<IDestinationCapacityService, DestinationCapacityService>();
        services.AddSingleton<IDestinationSecurityEvaluator, DestinationSecurityEvaluator>();
        services.AddSingleton<ILocalStorageService, LocalStorageService>();

        // M5 scan, delta inventory, and transfer state (real implementations; no
        // fakes).
        services.AddSingleton<IDeltaInventoryClient, DeltaInventoryClient>();
        services.AddSingleton<ITransferStateStore, SqliteTransferStateStore>();
        services.AddSingleton<IScanService, ScanService>();

        // M5 slice 2: temporary downloads, hashing, Graph item metadata, the transfer
        // engine, and copy orchestration. The "download" client is separate and
        // unauthenticated: no Graph bearer tokens and no cookies ever reach temporary
        // download hosts. The download retry coordinator is the single retry owner
        // for download requests; Graph requests keep the Graph coordinator.
        services.AddHttpClient(TemporaryDownloadClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
        services.AddSingleton<DownloadRetryCoordinator>();
        services.AddSingleton<ITemporaryDownloadClient, TemporaryDownloadClient>();
        services.AddSingleton<IHashingService, HashingService>();
        services.AddSingleton<IGraphMetadataClient, GraphMetadataClient>();
        services.AddSingleton<ITransferEngine, TransferEngine>();
        services.AddSingleton<ITransferOrchestrator, TransferOrchestrator>();

        // M6: per-run audit report generation (docs/REPORT_SCHEMA.md).
        services.AddSingleton<IReportWriter, ReportWriter>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ITransferStateSchemaInitializer, SqliteTransferStateSchemaInitializer>();

        return services;
    }
}
