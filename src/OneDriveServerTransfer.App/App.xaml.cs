using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneDriveServerTransfer.DependencyInjection;

namespace OneDriveServerTransfer;

/// <summary>
/// Application entry point. Builds the generic host (configuration, logging, dependency
/// injection) and resolves the single application window from the container.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        _host.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
            _host = null;
        }

        base.OnExit(e);
    }
}
