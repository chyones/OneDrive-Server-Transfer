using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.Configuration;
using OneDriveServerTransfer.DependencyInjection;
using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Verifies configuration loading from appsettings.example.json and that unsupported
/// state-schema or path-mapping versions are rejected by validation.
/// </summary>
public class ConfigurationLoadingTests
{
    private static string ExampleSettingsPath => Path.Combine(
        TestRepository.Root, "src", "OneDriveServerTransfer.App", "appsettings.example.json");

    private static IConfiguration LoadExampleConfiguration() => new ConfigurationBuilder()
        .AddJsonFile(ExampleSettingsPath, optional: false)
        .Build();

    [Fact]
    public void ExampleSettingsBindToVersionOneFoundation()
    {
        var configuration = LoadExampleConfiguration();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<TransferStateOptions>>().Value;
        Assert.Equal(1, options.SchemaVersion);
        Assert.Equal(1, options.PathMappingVersion);
    }

    [Fact]
    public void ExampleSettingsConfigureLocalStructuredFileLogging()
    {
        var configuration = LoadExampleConfiguration();

        var writeTo = configuration.GetSection("Serilog:WriteTo").GetChildren().ToArray();
        Assert.NotEmpty(writeTo);
        Assert.Contains(writeTo, sink => sink.GetValue<string>("Name") == "File");
        Assert.Equal("Information", configuration.GetValue<string>("Serilog:MinimumLevel:Default"));
    }

    [Fact]
    public void UnsupportedStateSchemaVersionIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TransferState:SchemaVersion"] = "2",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<TransferStateOptions>>().Value);
    }

    [Fact]
    public void UnsupportedPathMappingVersionIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TransferState:PathMappingVersion"] = "2",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<TransferStateOptions>>().Value);
    }

    [Fact]
    public void ExampleSettingsContainAuthenticationPlaceholdersOnly()
    {
        var configuration = LoadExampleConfiguration();

        var tenantId = configuration.GetValue<string>("Authentication:TenantId");
        var clientId = configuration.GetValue<string>("Authentication:ClientId");

        Assert.Equal("CONFIGURE_TENANT_ID", tenantId);
        Assert.Equal("CONFIGURE_CLIENT_ID", clientId);
        Assert.False(Guid.TryParse(tenantId, out _), "The committed example must not contain a real tenant GUID.");
        Assert.False(Guid.TryParse(clientId, out _), "The committed example must not contain a real client GUID.");
        Assert.Empty(configuration.GetSection("Authentication:AuthorizedOperatorObjectIds").Get<string[]>() ?? []);
    }

    [Fact]
    public void ExampleAuthenticationPlaceholdersFailValidationSafely()
    {
        var configuration = LoadExampleConfiguration();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value);
    }

    [Fact]
    public void ValidAuthenticationConfigurationBinds()
    {
        var operatorObjectId = "33333333-3333-3333-3333-333333333333";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:TenantId"] = "11111111-1111-1111-1111-111111111111",
                ["Authentication:ClientId"] = "22222222-2222-2222-2222-222222222222",
                ["Authentication:AuthorizedOperatorObjectIds:0"] = operatorObjectId,
                ["Authentication:RememberSignInDefault"] = "false",
                ["Authentication:RedirectUri"] = "http://localhost",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal("11111111-1111-1111-1111-111111111111", options.TenantId);
        Assert.Equal("22222222-2222-2222-2222-222222222222", options.ClientId);
        Assert.Equal([operatorObjectId], options.AuthorizedOperatorObjectIds);
        Assert.False(options.RememberSignInDefault);
        Assert.Equal("http://localhost", options.RedirectUri);
    }

    [Fact]
    public void ExampleSourceResolutionHostIsPlaceholderOnly()
    {
        var configuration = LoadExampleConfiguration();

        var host = configuration.GetValue<string>("SourceResolution:TenantOneDriveHost");

        Assert.Equal("CONFIGURE_TENANT_ONEDRIVE_HOST", host);
    }

    [Fact]
    public void ExampleSourceResolutionPlaceholderFailsValidationSafely()
    {
        var configuration = LoadExampleConfiguration();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SourceResolutionOptions>>().Value);
    }

    [Fact]
    public void ValidSourceResolutionConfigurationBinds()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceResolution:TenantOneDriveHost"] = "contoso-my.sharepoint.com",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SourceResolutionOptions>>().Value;
        Assert.Equal("contoso-my.sharepoint.com", options.TenantOneDriveHost);
    }
}
