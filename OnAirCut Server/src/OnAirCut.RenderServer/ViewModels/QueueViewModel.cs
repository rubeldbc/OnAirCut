using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using OnAirCut.RenderServer.Services;
using Serilog;

namespace OnAirCut.RenderServer.ViewModels;

public partial class QueueViewModel : ObservableObject, IDisposable
{
    private readonly SqliteRepository _repository;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private ProcessedStory? _selectedJob;

    [ObservableProperty]
    private bool _isRefreshing;

    public ObservableCollection<ProcessedStory> Jobs { get; } = [];

    public QueueViewModel(SqliteRepository repository)
    {
        _repository = repository;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;

        try
        {
            var stories = await _repository.SearchStoriesAsync(
                status: null, limit: 100);

            // Only show non-completed recent items (active queue)
            var activeItems = stories
                .Where(s => s.Status is not JobStatus.Completed)
                .ToList();

            // Keep completed items from today at the end
            var todayCompleted = stories
                .Where(s => s.Status == JobStatus.Completed && s.CreatedAt.Date == DateTime.Today)
                .Take(20)
                .ToList();

            Jobs.Clear();
            foreach (var item in activeItems.Concat(todayCompleted))
            {
                Jobs.Add(item);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to refresh queue");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RetryJobAsync()
    {
        if (SelectedJob == null) return;
        // Reset status to Pending in database
        await _repository.UpdateStoryStatusAsync(SelectedJob.JobId, JobStatus.Pending);
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelJob()
    {
        // Cancellation is handled by the orchestrator via CancellationToken
        // This is a UI placeholder
    }

    [RelayCommand]
    private void OpenOutput()
    {
        if (SelectedJob?.OutputFolderPath != null && Directory.Exists(SelectedJob.OutputFolderPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedJob.OutputFolderPath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void ViewDetail()
    {
        // Navigation to detail view handled in MainWindow
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
    }
}
