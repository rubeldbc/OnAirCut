using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OnAirCut.Recorder.Configuration;
using OnAirCut.Recorder.Services;
using OnAirCut.Recorder.ViewModels;
using OnAirCut.Recorder.Views;
using Serilog;

namespace OnAirCut.Recorder;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure logging first
        LoggingService.Configure();
        Log.Information("OnAirCut Recorder starting up");

        // Build DI container
        var services = new ServiceCollection();
        ServiceRegistration.ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Load settings
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Start shared folder health monitoring
        var sharedFolderService = _serviceProvider.GetRequiredService<SharedFolderHealthService>();
        sharedFolderService.StartMonitoring();

        // Start ad set watching
        var adSetProvider = _serviceProvider.GetRequiredService<AdSetProviderService>();
        adSetProvider.StartWatching();

        // Start history auto-refresh
        var historyVm = _serviceProvider.GetRequiredService<HistoryPanelViewModel>();
        historyVm.StartAutoRefresh();

        // Load ad sets
        var adSetVm = _serviceProvider.GetRequiredService<AdSetPanelViewModel>();
        await adSetVm.LoadAdSetsAsync();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("OnAirCut Recorder shutting down");
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
