using Microsoft.Extensions.DependencyInjection;
using OnAirCut.Core.Interfaces;
using OnAirCut.RenderServer.Services;
using OnAirCut.RenderServer.ViewModels;
using OnAirCut.RenderServer.Views;

namespace OnAirCut.RenderServer.Configuration;

public static class ServiceRegistration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<SharedFolderHealthService>();
        services.AddSingleton<ISharedFolderService>(sp => sp.GetRequiredService<SharedFolderHealthService>());
        services.AddSingleton<SqliteRepository>();
        services.AddSingleton<JobWatcherService>();
        services.AddSingleton<FileReadyChecker>();
        services.AddSingleton<FrameExtractionService>();
        services.AddSingleton<FfmpegCommandBuilder>();
        services.AddSingleton<FfmpegRenderService>();
        services.AddSingleton<OcrProcessor>();
        services.AddSingleton<OutputOrganizer>();
        services.AddSingleton<JobPipelineOrchestrator>();

        // Core interface implementations for Render Server
        services.AddSingleton<IAdSetProvider, FileBasedAdSetProvider>();
        services.AddSingleton<IOcrProfileProvider, FileBasedOcrProfileProvider>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<JobDetailViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
