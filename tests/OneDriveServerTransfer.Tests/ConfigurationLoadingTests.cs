using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Configuration;
using OneDriveServerTransfer.DependencyInjection;

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
}
