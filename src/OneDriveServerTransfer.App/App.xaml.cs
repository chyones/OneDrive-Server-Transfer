using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.DependencyInjection;

namespace OneDriveServerTransfer;

/// <summary>
/// Application entry point. Builds the generic host (configuration, logging, dependency
/// injection), validates authentication configuration fail-safe, and resolves the single
/// application window from the container.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddSingleton<MainWindow>();

            _host = builder.Build();
            _host.Start();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            await mainWindow.ViewModel.InitializeAsync().ConfigureAwait(true);
        }
        catch (OptionsValidationException exception)
        {
            var error = AuthenticationErrors.InvalidConfiguration(exception);
            MessageBox.Show(
                $"{error.Explanation}\n\n{error.CorrectiveAction}\n\nReference: {error.ReferenceCode}",
                error.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
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
