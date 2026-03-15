using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using OnAirCut.Core.Utilities;
using OnAirCut.Recorder.Models;
using OnAirCut.Recorder.Services;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class RecordingControlsViewModel : ObservableObject, IDisposable
{
    private readonly ISharedFolderService _sharedFolderService;
    private readonly ISettingsService _settingsService;
    private readonly JobSubmissionService _jobSubmissionService;
    private readonly TextCaptureService _textCaptureService;
    private readonly AdSetPanelViewModel _adSetPanel;
    private IVideoSource? _source;
    private Timer? _elapsedTimer;
    private DateTime _recordingStartTime;
    private bool _disposed;

    // Text capture state for the current recording session
    private readonly List<string> _capturedTexts = new();
    private string? _firstCapturedText;

    public RecordingControlsViewModel(
        ISharedFolderService sharedFolderService,
        ISettingsService settingsService,
        JobSubmissionService jobSubmissionService,
        TextCaptureService textCaptureService,
        AdSetPanelViewModel adSetPanel)
    {
        _sharedFolderService = sharedFolderService;
        _settingsService = settingsService;
        _jobSubmissionService = jobSubmissionService;
        _textCaptureService = textCaptureService;
        _adSetPanel = adSetPanel;
    }

    [ObservableProperty]
    private RecordingState _recordingState = RecordingState.Idle;

    [ObservableProperty]
    private TimeSpan _elapsedTime;

    [ObservableProperty]
    private string _currentClipPath = string.Empty;

    [ObservableProperty]
    private string _currentClipSize = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _lastCapturedText = string.Empty;

    [ObservableProperty]
    private int _capturedTextCount;

    [ObservableProperty]
    private string _lastSavedFilePath = string.Empty;

    /// <summary>
    /// Pending clips that have been recorded but not yet submitted as jobs.
    /// Operator can select ad set and submit these anytime.
    /// </summary>
    public ObservableCollection<PendingClip> PendingClips { get; } = new();

    [ObservableProperty]
    private PendingClip? _selectedPendingClip;

    [ObservableProperty]
    private int _pendingClipsCount;

    public event EventHandler? JobSubmitted;

    public void SetSource(IVideoSource? source)
    {
        _source = source;
        if (source is not null && source.IsConnected)
        {
            RecordingState = RecordingState.ReadyToRecord;
            StatusMessage = "Ready to record";
        }
        else
        {
            RecordingState = RecordingState.Idle;
            StatusMessage = "No source connected";
        }
    }

    /// <summary>
    /// Get the current MediaPlayer from whichever video source is active.
    /// All source implementations expose a public MediaPlayer property.
    /// </summary>
    private MediaPlayer? GetCurrentMediaPlayer()
    {
        return _source switch
        {
            LocalFileSource local => local.MediaPlayer,
            YouTubeSource yt => yt.MediaPlayer,
            LiveFeedSource live => live.MediaPlayer,
            _ => null
        };
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (_source is null || !_source.IsConnected)
        {
            StatusMessage = "No source connected";
            return;
        }

        try
        {
            // Clear text capture state for the new recording session
            _capturedTexts.Clear();
            _firstCapturedText = null;
            LastCapturedText = string.Empty;
            CapturedTextCount = 0;

            var dateFolder = _sharedFolderService.GetDateSubfolder(FolderNames.IngestRawClips, DateTime.Now);
            var fileName = $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.{_settingsService.Settings.RecordingFormat}";
            var outputPath = Path.Combine(dateFolder, fileName);

            await _source.StartRecordingAsync(outputPath);
            CurrentClipPath = outputPath;
            RecordingState = RecordingState.Recording;
            StatusMessage = "Recording...";
            _recordingStartTime = DateTime.Now;

            _elapsedTimer?.Dispose();
            _elapsedTimer = new Timer(_ =>
            {
                ElapsedTime = DateTime.Now - _recordingStartTime;
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Log.Information("Recording started: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start recording");
            StatusMessage = $"Error: {ex.Message}";
            RecordingState = RecordingState.ReadyToRecord;
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        if (_source is null || !_source.IsRecording) return;

        try
        {
            _elapsedTimer?.Dispose();
            _elapsedTimer = null;

            StatusMessage = "Finalizing clip...";
            var result = await _source.StopRecordingAsync();

            // If we captured text during this session, rename the file and create .txt
            if (_capturedTexts.Count > 0 && !string.IsNullOrEmpty(_firstCapturedText))
            {
                result = await RenameClipWithCapturedTextAsync(result);
            }

            // Add to pending clips queue instead of blocking for submission
            var clip = new PendingClip
            {
                RecordingResult = result,
                RecordedAt = DateTime.Now,
                FileName = Path.GetFileName(result.FilePath),
                Duration = TimeSpan.FromSeconds(result.DurationSeconds),
                FileSize = FormatFileSize(result.FileSizeBytes),
                SelectedAdSet = _adSetPanel.SelectedAdSetName
            };
            PendingClips.Insert(0, clip);
            PendingClipsCount = PendingClips.Count;

            // Immediately ready for next recording
            RecordingState = RecordingState.ReadyToRecord;
            ElapsedTime = TimeSpan.Zero;
            StatusMessage = $"Clip saved. {PendingClips.Count} pending. Ready to record next.";

            // Reset text capture state
            _capturedTexts.Clear();
            _firstCapturedText = null;
            LastCapturedText = string.Empty;
            CapturedTextCount = 0;

            Log.Information("Recording stopped: {Path}, Duration: {Duration}s, Pending: {Count}",
                result.FilePath, result.DurationSeconds, PendingClips.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to stop recording");
            StatusMessage = $"Error: {ex.Message}";
            RecordingState = RecordingState.ReadyToRecord;
        }
    }

    /// <summary>
    /// Rename the recorded clip file using the first captured OCR text,
    /// and create a companion .txt file with all captured texts.
    /// </summary>
    private async Task<RecordingResult> RenameClipWithCapturedTextAsync(RecordingResult result)
    {
        try
        {
            var directory = Path.GetDirectoryName(result.FilePath);
            if (string.IsNullOrEmpty(directory)) return result;

            var extension = Path.GetExtension(result.FilePath);
            var safeName = TitleSanitizer.ToSafeFolderName(_firstCapturedText);

            // Ensure unique filename
            safeName = TitleSanitizer.EnsureUniqueName(safeName, name =>
                File.Exists(Path.Combine(directory, name + extension)));

            var newVideoPath = Path.Combine(directory, safeName + extension);
            var txtPath = Path.Combine(directory, safeName + ".txt");

            // Rename the video file
            if (File.Exists(result.FilePath) && result.FilePath != newVideoPath)
            {
                File.Move(result.FilePath, newVideoPath);
                Log.Information("Renamed clip: {Old} -> {New}", result.FilePath, newVideoPath);
            }

            // Create the .txt file with all captured texts
            var textContent = string.Join(Environment.NewLine, _capturedTexts);
            await File.WriteAllTextAsync(txtPath, textContent);
            LastSavedFilePath = newVideoPath;
            Log.Information("Created text file: {Path} with {Count} lines", txtPath, _capturedTexts.Count);

            // Update the result with the new path
            return new RecordingResult
            {
                FilePath = newVideoPath,
                DurationSeconds = result.DurationSeconds,
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                FileSizeBytes = result.FileSizeBytes
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to rename clip with captured text");
            return result;
        }
    }

    [ObservableProperty]
    private bool _isCapturing;

    [RelayCommand]
    private async Task CaptureTextAsync()
    {
        if (_source is null || !_source.IsConnected)
        {
            StatusMessage = "No source connected";
            return;
        }

        // Block rapid clicks — only one capture at a time
        if (IsCapturing) return;
        IsCapturing = true;

        try
        {
            StatusMessage = "Capturing text...";

            var mediaPlayer = GetCurrentMediaPlayer();

            // Single clean capture — take one snapshot and OCR it
            var frame = await _textCaptureService.CaptureFrameAsync(mediaPlayer);
            if (frame is null)
            {
                StatusMessage = "Failed to capture frame";
                return;
            }

            var finalText = await _textCaptureService.CaptureTextFromFrameAsync(frame);

            if (string.IsNullOrWhiteSpace(finalText))
            {
                StatusMessage = "No text detected";
                LastCapturedText = string.Empty;
                return;
            }

            LastCapturedText = finalText;

            if (RecordingState == RecordingState.Recording)
            {
                _capturedTexts.Add(finalText);
                if (_capturedTexts.Count == 1)
                    _firstCapturedText = finalText;

                CapturedTextCount = _capturedTexts.Count;
                StatusMessage = "Text captured";
                Log.Information("Text captured during recording ({Count}): {Text}", CapturedTextCount, finalText);
            }
            else
            {
                await SaveStandaloneTextFileAsync(finalText);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Text capture failed");
            StatusMessage = $"Capture error: {ex.Message}";
        }
        finally
        {
            IsCapturing = false;
        }
    }

    /// <summary>
    /// When not recording, save captured text as a standalone .txt file in the Output date folder.
    /// </summary>
    private async Task SaveStandaloneTextFileAsync(string text)
    {
        try
        {
            var safeName = TitleSanitizer.ToSafeFolderName(text);
            var dateFolder = _sharedFolderService.GetDateSubfolder(FolderNames.Output, DateTime.Now);

            // Ensure unique filename
            safeName = TitleSanitizer.EnsureUniqueName(safeName, name =>
                File.Exists(Path.Combine(dateFolder, name + ".txt")));

            var txtPath = Path.Combine(dateFolder, safeName + ".txt");
            await File.WriteAllTextAsync(txtPath, text);

            LastSavedFilePath = txtPath;
            StatusMessage = "Text captured";
            Log.Information("Standalone text file saved: {Path}", txtPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save standalone text file");
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelClip()
    {
        if (RecordingState == RecordingState.Recording && _source?.IsRecording == true)
        {
            _elapsedTimer?.Dispose();
            _ = _source.StopRecordingAsync();
            // Don't add to pending
        }

        RecordingState = _source is not null && _source.IsConnected
            ? RecordingState.ReadyToRecord
            : RecordingState.Idle;

        StatusMessage = "Clip cancelled";
        ElapsedTime = TimeSpan.Zero;

        // Clear text capture state
        _capturedTexts.Clear();
        _firstCapturedText = null;
        LastCapturedText = string.Empty;
        CapturedTextCount = 0;
    }

    [RelayCommand]
    private async Task SubmitPendingClipAsync(PendingClip? clip)
    {
        clip ??= SelectedPendingClip;
        if (clip is null)
        {
            StatusMessage = "No clip selected";
            return;
        }

        try
        {
            StatusMessage = $"Submitting: {clip.FileName}...";
            var ocrProfile = _settingsService.Settings.OcrProfileName;
            var jobId = await _jobSubmissionService.SubmitJobAsync(
                clip.RecordingResult, clip.SelectedAdSet, ocrProfile);

            PendingClips.Remove(clip);
            PendingClipsCount = PendingClips.Count;
            StatusMessage = $"Submitted: {jobId}. {PendingClips.Count} pending.";

            JobSubmitted?.Invoke(this, EventArgs.Empty);
            Log.Information("Job submitted: {JobId} for {File}", jobId, clip.FileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to submit job for {File}", clip.FileName);
            StatusMessage = $"Submit failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SubmitAllPendingAsync()
    {
        var clips = PendingClips.ToList();
        foreach (var clip in clips)
        {
            await SubmitPendingClipAsync(clip);
        }
    }

    [RelayCommand]
    private void RemovePendingClip(PendingClip? clip)
    {
        if (clip is null) return;
        PendingClips.Remove(clip);
        PendingClipsCount = PendingClips.Count;
        StatusMessage = $"Clip removed. {PendingClips.Count} pending.";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _elapsedTimer?.Dispose();
    }
}

/// <summary>
/// Represents a recorded clip waiting for ad set selection and job submission.
/// </summary>
public partial class PendingClip : ObservableObject
{
    public RecordingResult RecordingResult { get; init; } = null!;
    public DateTime RecordedAt { get; init; }
    public string FileName { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string FileSize { get; init; } = string.Empty;

    [ObservableProperty]
    private string? _selectedAdSet;
}
