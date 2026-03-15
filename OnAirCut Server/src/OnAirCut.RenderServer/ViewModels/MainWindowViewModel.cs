using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Interfaces;
using OnAirCut.RenderServer.Services;

namespace OnAirCut.RenderServer.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly ISharedFolderService _sharedFolderService;
    private readonly JobWatcherService _jobWatcher;
    private readonly DispatcherTimer _clockTimer;

    [ObservableProperty]
    private string _title = "OnAirCut Render Server";

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

    [ObservableProperty]
    private bool _isSharedFolderHealthy;

    [ObservableProperty]
    private string _sharedFolderStatus = "Not Connected";

    [ObservableProperty]
    private int _pendingJobsCount;

    [ObservableProperty]
    private int _completedTodayCount;

    [ObservableProperty]
    private int _selectedTabIndex;

    public DashboardViewModel DashboardViewModel { get; }
    public QueueViewModel QueueViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public LogsViewModel LogsViewModel { get; }

    public MainWindowViewModel(
        ISharedFolderService sharedFolderService,
        JobWatcherService jobWatcher,
        DashboardViewModel dashboardViewModel,
        QueueViewModel queueViewModel,
        HistoryViewModel historyViewModel,
        SettingsViewModel settingsViewModel,
        LogsViewModel logsViewModel)
    {
        _sharedFolderService = sharedFolderService;
        _jobWatcher = jobWatcher;

        DashboardViewModel = dashboardViewModel;
        QueueViewModel = queueViewModel;
        HistoryViewModel = historyViewModel;
        SettingsViewModel = settingsViewModel;
        LogsViewModel = logsViewModel;

        _sharedFolderService.HealthChanged += OnHealthChanged;
        _jobWatcher.PendingCountChanged += OnPendingCountChanged;

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) =>
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        };
        _clockTimer.Start();

        // Initial health check
        IsSharedFolderHealthy = _sharedFolderService.IsHealthy;
        SharedFolderStatus = IsSharedFolderHealthy ? "Connected" : (_sharedFolderService.LastError ?? "Not Connected");
    }

    private void OnHealthChanged(object? sender, SharedFolderHealthChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsSharedFolderHealthy = e.IsHealthy;
            SharedFolderStatus = e.IsHealthy ? "Connected" : (e.ErrorMessage ?? "Disconnected");
        });
    }

    private void OnPendingCountChanged(object? sender, int count)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            PendingJobsCount = count;
        });
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SelectedTabIndex = 3; // Settings tab
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _sharedFolderService.HealthChanged -= OnHealthChanged;
        _jobWatcher.PendingCountChanged -= OnPendingCountChanged;
    }
}
