using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using OnAirCut.RenderServer.Services;
using Serilog;

namespace OnAirCut.RenderServer.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly SqliteRepository _repository;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private string? _selectedStatus;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<ProcessedStory> Stories { get; } = [];

    public List<string> StatusOptions { get; } =
    [
        "All",
        nameof(JobStatus.Pending),
        nameof(JobStatus.Processing),
        nameof(JobStatus.Completed),
        nameof(JobStatus.Failed)
    ];

    public HistoryViewModel(SqliteRepository repository)
    {
        _repository = repository;
        SelectedStatus = "All";
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsLoading = true;
        try
        {
            JobStatus? statusFilter = null;
            if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "All" &&
                Enum.TryParse<JobStatus>(SelectedStatus, out var parsed))
            {
                statusFilter = parsed;
            }

            var results = await _repository.SearchStoriesAsync(
                searchText: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                dateFrom: DateFrom,
                dateTo: DateTo,
                status: statusFilter);

            Stories.Clear();
            foreach (var story in results)
            {
                Stories.Add(story);
            }
            TotalCount = Stories.Count;
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
    private async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        DateFrom = null;
        DateTo = null;
        SelectedStatus = "All";
        await SearchAsync();
    }

    [RelayCommand]
    private void OpenOutputFolder(ProcessedStory? story)
    {
        if (story?.OutputFolderPath != null && Directory.Exists(story.OutputFolderPath))
        {
            Process.Start(new ProcessStartInfo { FileName = story.OutputFolderPath, UseShellExecute = true });
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"OnAirCut_History_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("JobId,Title,Source,Status,Duration,SubmittedAt,ProcessedAt,OutputPath");

                foreach (var story in Stories)
                {
                    sb.AppendLine($"\"{story.JobId}\",\"{story.TitleNormalized}\",\"{story.SourceName}\"," +
                                  $"\"{story.Status}\",{story.DurationSeconds:F1}," +
                                  $"\"{story.SubmittedAt:O}\",\"{story.ProcessedAt:O}\"," +
                                  $"\"{story.OutputVideoPath}\"");
                }

                await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
                Log.Information("Exported {Count} records to {Path}", Stories.Count, dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CSV export failed");
        }
    }
}
