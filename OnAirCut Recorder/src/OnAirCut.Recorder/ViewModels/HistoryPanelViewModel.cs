using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using OnAirCut.Recorder.Services;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class HistoryPanelViewModel : ObservableObject, IDisposable
{
    private readonly HistoryService _historyService;
    private Timer? _refreshTimer;
    private bool _disposed;

    public HistoryPanelViewModel(HistoryService historyService)
    {
        _historyService = historyService;
        AvailableStatuses = new ObservableCollection<string>(
            ["All", .. Enum.GetNames<JobStatus>()]);
    }

    [ObservableProperty]
    private ObservableCollection<ProcessedStory> _stories = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private string _selectedStatus = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<string> AvailableStatuses { get; }

    public void StartAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = new Timer(async _ =>
        {
            try
            {
                await SearchInternalAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Auto-refresh failed");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await SearchInternalAsync();
    }

    private async Task SearchInternalAsync()
    {
        IsLoading = true;
        try
        {
            JobStatus? statusFilter = null;
            if (SelectedStatus != "All" && Enum.TryParse<JobStatus>(SelectedStatus, out var parsed))
            {
                statusFilter = parsed;
            }

            var results = await _historyService.SearchAsync(
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                DateFrom, DateTo, statusFilter);

            Stories = new ObservableCollection<ProcessedStory>(results);
            TotalCount = results.Count;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Search failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        DateFrom = null;
        DateTo = null;
        SelectedStatus = "All";
        _ = SearchAsync();
    }

    [RelayCommand]
    private void OpenOutputFolder(ProcessedStory? story)
    {
        if (story?.OutputFolderPath is not null && Directory.Exists(story.OutputFolderPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = story.OutputFolderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open output folder");
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await SearchInternalAsync();
    }

    [RelayCommand]
    private void OpenFile(ProcessedStory? story)
    {
        var path = story?.OutputVideoPath;
        if (path is not null && File.Exists(path))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { Log.Error(ex, "Failed to open file"); }
        }
    }

    [RelayCommand]
    private void OpenFileLocation(ProcessedStory? story)
    {
        var path = story?.OutputVideoPath;
        if (path is not null && File.Exists(path))
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex) { Log.Error(ex, "Failed to open file location"); }
        }
        else if (story?.OutputFolderPath is not null && Directory.Exists(story.OutputFolderPath))
        {
            OpenOutputFolder(story);
        }
    }

    [RelayCommand]
    private void RemoveFromList(ProcessedStory? story)
    {
        if (story is null) return;
        Stories.Remove(story);
        TotalCount = Stories.Count;
    }

    [RelayCommand]
    private void RemoveFromDisk(ProcessedStory? story)
    {
        if (story is null) return;

        // Delete output video
        if (story.OutputVideoPath is not null && File.Exists(story.OutputVideoPath))
        {
            try { File.Delete(story.OutputVideoPath); }
            catch (Exception ex) { Log.Warning(ex, "Failed to delete output video"); }
        }

        // Delete output folder if empty
        if (story.OutputFolderPath is not null && Directory.Exists(story.OutputFolderPath))
        {
            try
            {
                var remaining = Directory.GetFiles(story.OutputFolderPath).Length;
                if (remaining == 0)
                    Directory.Delete(story.OutputFolderPath, true);
            }
            catch (Exception ex) { Log.Warning(ex, "Failed to delete output folder"); }
        }

        Stories.Remove(story);
        TotalCount = Stories.Count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer?.Dispose();
    }
}
