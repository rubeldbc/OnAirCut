using Microsoft.Extensions.DependencyInjection;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Recorder.Services;
using OnAirCut.Recorder.ViewModels;
using OnAirCut.Recorder.Views;

namespace OnAirCut.Recorder.Configuration;

public static class ServiceRegistration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<SharedFolderHealthService>();
        services.AddSingleton<ISharedFolderService>(sp => sp.GetRequiredService<SharedFolderHealthService>());
        services.AddSingleton<AdSetProviderService>();
        services.AddSingleton<IAdSetProvider>(sp => sp.GetRequiredService<AdSetProviderService>());
        services.AddSingleton<JobSubmissionService>();
        services.AddSingleton<TextCaptureService>();
        services.AddSingleton<HistoryService>();

        // Video source factory
        services.AddTransient<LocalFileSource>();
        services.AddTransient<LiveFeedSource>();
        services.AddTransient<YouTubeSource>();
        services.AddSingleton<Func<SourceType, IVideoSource>>(sp => sourceType =>
        {
            return sourceType switch
            {
                SourceType.LocalFile => sp.GetRequiredService<LocalFileSource>(),
                SourceType.LiveFeed => sp.GetRequiredService<LiveFeedSource>(),
                SourceType.YouTubeUrl => sp.GetRequiredService<YouTubeSource>(),
                _ => sp.GetRequiredService<LocalFileSource>()
            };
        });

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<SourcePanelViewModel>();
        services.AddSingleton<PreviewPlayerViewModel>();
        services.AddSingleton<RecordingControlsViewModel>();
        services.AddSingleton<AdSetPanelViewModel>();
        services.AddSingleton<HistoryPanelViewModel>();
        services.AddSingleton<AdvertiseManagerViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<OcrRegionViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
