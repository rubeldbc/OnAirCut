using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using OnAirCut.RenderServer.Services;
using Serilog;

namespace OnAirCut.RenderServer.ViewModels;

public partial class JobDetailViewModel : ObservableObject
{
    private readonly SqliteRepository _repository;

    [ObservableProperty]
    private string _jobId = string.Empty;

    [ObservableProperty]
    private string _titleRaw = string.Empty;

    [ObservableProperty]
    private string _titleNormalized = string.Empty;

    [ObservableProperty]
    private string _safeFolderName = string.Empty;

    [ObservableProperty]
    private string _sourceName = string.Empty;

    [ObservableProperty]
    private string _sourceType = string.Empty;

    [ObservableProperty]
    private string _adSetName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private double _ocrConfidence;

    [ObservableProperty]
    private string _outputVideoPath = string.Empty;

    [ObservableProperty]
    private string _outputFolderPath = string.Empty;

    [ObservableProperty]
    private string _rawClipPath = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _submittedAt = string.Empty;

    [ObservableProperty]
    private string _processedAt = string.Empty;

    // --- Ad config details ---
    [ObservableProperty] private string _adConfigSummary = string.Empty;
    [ObservableProperty] private bool _hasAdConfig;

    public ObservableCollection<StepLogEntry> StepLogs { get; } = [];
    public ObservableCollection<OcrResultEntry> OcrResults { get; } = [];
    public ObservableCollection<string> FrameThumbnails { get; } = [];

    private readonly IAdSetProvider _adSetProvider;

    public JobDetailViewModel(SqliteRepository repository, IAdSetProvider adSetProvider)
    {
        _repository = repository;
        _adSetProvider = adSetProvider;
    }

    public async Task LoadJobAsync(string jobId)
    {
        try
        {
            var story = await _repository.GetStoryByJobIdAsync(jobId);
            if (story == null) return;

            JobId = story.JobId;
            TitleRaw = story.TitleRaw ?? string.Empty;
            TitleNormalized = story.TitleNormalized ?? string.Empty;
            SafeFolderName = story.SafeFolderName ?? string.Empty;
            SourceName = story.SourceName ?? string.Empty;
            SourceType = story.SourceType.ToString();
            AdSetName = story.AdSetName ?? string.Empty;
            await LoadAdConfigSummaryAsync(story);
            Status = story.Status.ToString();
            ErrorMessage = story.ErrorMessage ?? string.Empty;
            OcrConfidence = story.OcrConfidence ?? 0;
            OutputVideoPath = story.OutputVideoPath ?? string.Empty;
            OutputFolderPath = story.OutputFolderPath ?? string.Empty;
            RawClipPath = story.RawClipPath;
            DurationText = TimeSpan.FromSeconds(story.DurationSeconds).ToString(@"mm\:ss");
            SubmittedAt = story.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss");
            ProcessedAt = story.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

            // Load step logs
            StepLogs.Clear();
            var logs = await _repository.GetJobLogsAsync(jobId);
            foreach (var (step, status, message, startedAt, completedAt, durationMs) in logs)
            {
                StepLogs.Add(new StepLogEntry
                {
                    Step = step,
                    Status = status,
                    Message = message ?? string.Empty,
                    StartedAt = startedAt?.ToString("HH:mm:ss") ?? "",
                    Duration = durationMs.HasValue ? $"{durationMs}ms" : ""
                });
            }

            // Load OCR results
            OcrResults.Clear();
            var ocrResults = await _repository.GetOcrResultsAsync(jobId);
            foreach (var (frameIndex, framePath, rawText, confidence, profileUsed) in ocrResults)
            {
                OcrResults.Add(new OcrResultEntry
                {
                    FrameIndex = frameIndex,
                    RawText = rawText ?? string.Empty,
                    Confidence = confidence,
                    ProfileUsed = profileUsed ?? string.Empty
                });
            }

            // Load frame thumbnails
            FrameThumbnails.Clear();
            if (!string.IsNullOrEmpty(story.FramesPath) && Directory.Exists(story.FramesPath))
            {
                foreach (var frame in Directory.GetFiles(story.FramesPath, "*.jpg").OrderBy(f => f).Take(20))
                {
                    FrameThumbnails.Add(frame);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load job detail for {JobId}", jobId);
        }
    }

    private async Task LoadAdConfigSummaryAsync(ProcessedStory story)
    {
        HasAdConfig = false;
        AdConfigSummary = string.Empty;

        try
        {
            // Try from embedded JSON first, then from provider
            AdSetConfig? config = null;
            if (!string.IsNullOrEmpty(story.AdSetConfigJson))
            {
                config = JsonSerializer.Deserialize<AdSetConfig>(story.AdSetConfigJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (!string.IsNullOrEmpty(story.AdSetName))
            {
                config = await _adSetProvider.GetAdSetByNameAsync(story.AdSetName);
            }

            if (config is null) return;

            var lines = new List<string>();
            if (config.Doggy is { } d)
                lines.Add($"Doggy: {(d.Enabled ? "ON" : "OFF")} | {d.File ?? "—"} | Pos({d.PositionX:F0},{d.PositionY:F0}) Size({d.Width:F0}x{d.Height:F0}) Opacity({d.Opacity:F1})");
            if (config.Popup is { } p)
                lines.Add($"Popup: {(p.Enabled ? "ON" : "OFF")} | {p.File ?? "—"} | Play {p.TotalPlay}x @ {p.DurationPerTime:F1}s | Pos({p.PositionX:F0},{p.PositionY:F0}) Size({p.Width:F0}x{p.Height:F0})");
            if (config.Tvc is { } t)
                lines.Add($"TVC: {(t.Enabled ? "ON" : "OFF")} | {t.File ?? "—"} | Insert {t.Count}x");

            if (lines.Count > 0)
            {
                HasAdConfig = true;
                AdConfigSummary = string.Join("\n", lines);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load ad config summary");
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        if (!string.IsNullOrEmpty(JobId))
        {
            await _repository.UpdateStoryStatusAsync(JobId, JobStatus.Pending);
            Status = JobStatus.Pending.ToString();
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (!string.IsNullOrEmpty(OutputFolderPath) && Directory.Exists(OutputFolderPath))
        {
            Process.Start(new ProcessStartInfo { FileName = OutputFolderPath, UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void OpenRawClip()
    {
        if (!string.IsNullOrEmpty(RawClipPath) && File.Exists(RawClipPath))
        {
            Process.Start(new ProcessStartInfo { FileName = RawClipPath, UseShellExecute = true });
        }
    }
}

public class StepLogEntry
{
    public string Step { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StartedAt { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}

public class OcrResultEntry
{
    public int FrameIndex { get; set; }
    public string RawText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string ProfileUsed { get; set; } = string.Empty;
}
