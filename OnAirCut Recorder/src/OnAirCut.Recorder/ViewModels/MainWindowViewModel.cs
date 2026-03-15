using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Interfaces;
using OnAirCut.Recorder.Services;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly ISharedFolderService _sharedFolderService;
    private Timer? _clockTimer;
    private bool _disposed;

    public MainWindowViewModel(
        ISharedFolderService sharedFolderService,
        SourcePanelViewModel sourcePanelViewModel,
        PreviewPlayerViewModel previewPlayerViewModel,
        RecordingControlsViewModel recordingControlsViewModel,
        AdSetPanelViewModel adSetPanelViewModel,
        HistoryPanelViewModel historyPanelViewModel,
        SettingsViewModel settingsViewModel)
    {
        _sharedFolderService = sharedFolderService;
        SourcePanel = sourcePanelViewModel;
        PreviewPlayer = previewPlayerViewModel;
        RecordingControls = recordingControlsViewModel;
        AdSetPanel = adSetPanelViewModel;
        HistoryPanel = historyPanelViewModel;
        Settings = settingsViewModel;

        _sharedFolderService.HealthChanged += OnSharedFolderHealthChanged;
        SourcePanel.SourceChanged += OnSourceChanged;

        // Read initial health state (may already be healthy by the time VM is created)
        IsSharedFolderHealthy = _sharedFolderService.IsHealthy;
        SharedFolderStatus = _sharedFolderService.IsHealthy ? "Connected" : "Not Connected";

        StartClockTimer();
    }

    [ObservableProperty]
    private string _title = "OnAirCut Recorder";

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

    [ObservableProperty]
    private bool _isSharedFolderHealthy;

    [ObservableProperty]
    private string _sharedFolderStatus = "Not Connected";

    [ObservableProperty]
    private string _sourceStatus = "No Source";

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private TimeSpan _clipDuration;

    [ObservableProperty]
    private bool _isSettingsVisible;

    public SourcePanelViewModel SourcePanel { get; }
    public PreviewPlayerViewModel PreviewPlayer { get; }
    public RecordingControlsViewModel RecordingControls { get; }
    public AdSetPanelViewModel AdSetPanel { get; }
    public HistoryPanelViewModel HistoryPanel { get; }
    public SettingsViewModel Settings { get; }

    private void StartClockTimer()
    {
        _clockTimer = new Timer(_ =>
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void OnSharedFolderHealthChanged(object? sender, SharedFolderHealthChangedEventArgs e)
    {
        IsSharedFolderHealthy = e.IsHealthy;
        SharedFolderStatus = e.IsHealthy ? "Connected" : (e.ErrorMessage ?? "Disconnected");
    }

    private void OnSourceChanged(object? sender, IVideoSource? source)
    {
        if (source is not null)
        {
            SourceStatus = $"{source.SourceType}: {source.SourceName}";
            PreviewPlayer.SetSource(source);
            RecordingControls.SetSource(source);
        }
        else
        {
            SourceStatus = "No Source";
            PreviewPlayer.SetSource(null);
            RecordingControls.SetSource(null);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsVisible = !IsSettingsVisible;
    }

    [RelayCommand]
    private void NavigateToTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out var index))
        {
            SelectedTabIndex = index;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clockTimer?.Dispose();
        PreviewPlayer.Dispose();
        RecordingControls.Dispose();
        HistoryPanel.Dispose();
    }
}
