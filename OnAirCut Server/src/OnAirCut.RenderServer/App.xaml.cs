using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OnAirCut.RenderServer.Configuration;
using OnAirCut.RenderServer.Services;
using OnAirCut.RenderServer.ViewModels;
using OnAirCut.RenderServer.Views;
using Serilog;

namespace OnAirCut.RenderServer;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure logging first
        LoggingService.Configure();
        Log.Information("OnAirCut Render Server starting...");

        // Build DI container
        var services = new ServiceCollection();
        ServiceRegistration.ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Initialize settings
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Initialize SQLite
        var repository = _serviceProvider.GetRequiredService<SqliteRepository>();
        try
        {
            await repository.InitializeAsync();
            Log.Information("Database initialized");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize database");
        }

        // Start shared folder monitoring
        var sharedFolderService = _serviceProvider.GetRequiredService<SharedFolderHealthService>();
        sharedFolderService.StartMonitoring();

        // Start job watcher
        var jobWatcher = _serviceProvider.GetRequiredService<JobWatcherService>();
        jobWatcher.StartWatching();

        // Start pipeline orchestrator
        var orchestrator = _serviceProvider.GetRequiredService<JobPipelineOrchestrator>();
        orchestrator.Start();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.Show();

        Log.Information("OnAirCut Render Server started successfully");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("OnAirCut Render Server shutting down...");

        if (_serviceProvider != null)
        {
            // Stop services
            try
            {
                var orchestrator = _serviceProvider.GetRequiredService<JobPipelineOrchestrator>();
                orchestrator.Stop();
            }
            catch { /* ignore shutdown errors */ }

            try
            {
                var jobWatcher = _serviceProvider.GetRequiredService<JobWatcherService>();
                jobWatcher.StopWatching();
            }
            catch { /* ignore shutdown errors */ }

            _serviceProvider.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
