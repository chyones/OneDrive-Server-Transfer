using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OneDriveServerTransfer.Abstractions;
using OneDriveServerTransfer.Authentication;
using OneDriveServerTransfer.DependencyInjection;

namespace OneDriveServerTransfer;

/// <summary>
/// Application entry point. Builds the generic host (configuration, logging, dependency
/// injection), validates authentication configuration fail-safe, and resolves the single
/// application window from the container. Startup failures surface as reference-coded
/// user-facing errors, never as unhandled crashes.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private MainWindow? _mainWindow;

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

            _mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _mainWindow.Show();
            await _mainWindow.ViewModel.InitializeAsync().ConfigureAwait(true);
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
        catch (Exception exception)
        {
            // Startup failures never crash raw: the operator gets the generic
            // reference-coded error and no exception details reach the screen.
            var error = UserInterfaceErrors.Unexpected(exception);
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
        // Release the exclusive destination lock before the host stops.
        _mainWindow?.ViewModel.Dispose();

        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
            _host = null;
        }

        base.OnExit(e);
    }
}
