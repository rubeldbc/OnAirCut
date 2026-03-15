using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OnAirCut.Core.Enums;
using OnAirCut.RenderServer.Services;
using Serilog;

namespace OnAirCut.RenderServer.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SqliteRepository _repository;
    private readonly JobPipelineOrchestrator _orchestrator;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private string _currentJobId = string.Empty;

    [ObservableProperty]
    private string _currentStep = string.Empty;

    [ObservableProperty]
    private double _renderProgress;

    [ObservableProperty]
    private string _renderSpeed = "N/A";

    [ObservableProperty]
    private string _renderEta = "--:--:--";

    [ObservableProperty]
    private bool _isProcessing;

    public ObservableCollection<string> RecentActivities { get; } = [];

    public DashboardViewModel(SqliteRepository repository, JobPipelineOrchestrator orchestrator)
    {
        _repository = repository;
        _orchestrator = orchestrator;

        _orchestrator.JobProgressChanged += OnJobProgress;
        _orchestrator.JobCompleted += OnJobCompleted;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshStatsAsync();
        _refreshTimer.Start();
    }

    private void OnJobProgress(object? sender, JobProgressEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentJobId = e.JobId;
            CurrentStep = e.Step;
            RenderProgress = e.Progress;
            RenderSpeed = e.Speed ?? "N/A";
            IsProcessing = e.Status is not (JobStatus.Completed or JobStatus.Failed);

            if (e.Progress > 0 && e.Progress < 100 && e.Speed != null && e.Speed != "N/A")
            {
                try
                {
                    var speedVal = double.Parse(e.Speed.Replace("x", ""));
                    if (speedVal > 0)
                    {
                        var remaining = (100 - e.Progress) / 100.0 * (_orchestrator.CurrentJob?.InputDuration ?? 0) / speedVal;
                        RenderEta = TimeSpan.FromSeconds(remaining).ToString(@"hh\:mm\:ss");
                    }
                }
                catch
                {
                    RenderEta = "--:--:--";
                }
            }
        });
    }

    private void OnJobCompleted(object? sender, JobCompletedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var status = e.Success ? "Completed" : $"Failed: {e.Error}";
            var activity = $"[{DateTime.Now:HH:mm:ss}] {e.JobId} - {status}";
            RecentActivities.Insert(0, activity);
            while (RecentActivities.Count > 50)
                RecentActivities.RemoveAt(RecentActivities.Count - 1);

            if (!_orchestrator.IsRunning || _orchestrator.CurrentJob == null)
            {
                IsProcessing = false;
                CurrentJobId = string.Empty;
                CurrentStep = string.Empty;
                RenderProgress = 0;
                RenderSpeed = "N/A";
                RenderEta = "--:--:--";
            }
        });
    }

    private async Task RefreshStatsAsync()
    {
        try
        {
            var stats = await _repository.GetTodayStatsAsync();
            PendingCount = stats.PendingCount;
            ActiveCount = stats.ProcessingCount;
            CompletedCount = stats.CompletedCount;
            FailedCount = stats.FailedCount;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to refresh dashboard stats");
        }
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _orchestrator.JobProgressChanged -= OnJobProgress;
        _orchestrator.JobCompleted -= OnJobCompleted;
    }
}
