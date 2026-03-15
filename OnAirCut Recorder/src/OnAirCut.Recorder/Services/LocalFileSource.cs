using System.IO;
using CliWrap;
using CliWrap.Buffered;
using LibVLCSharp.Shared;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class LocalFileSource : IVideoSource
{
    private readonly ISettingsService _settingsService;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private string _sourceUri = string.Empty;
    private bool _isConnected;
    private bool _isRecording;
    private DateTime _recordingStartTime;
    private TimeSpan _recordingStartPosition;
    private string _recordingOutputPath = string.Empty;
    private bool _disposed;

    public LocalFileSource(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public SourceType SourceType => SourceType.LocalFile;
    public string SourceName => Path.GetFileName(_sourceUri);
    public bool IsConnected => _isConnected;
    public bool IsRecording => _isRecording;
    public bool IsHealthy => _isConnected;

    public TimeSpan CurrentPosition =>
        _mediaPlayer is not null
            ? TimeSpan.FromMilliseconds(_mediaPlayer.Time)
            : TimeSpan.Zero;

    public TimeSpan? Duration =>
        _mediaPlayer?.Length > 0
            ? TimeSpan.FromMilliseconds(_mediaPlayer.Length)
            : null;

    public event EventHandler<SourceHealthChangedEventArgs>? HealthChanged;
    public event EventHandler<SourcePositionChangedEventArgs>? PositionChanged;

    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public Task<bool> ConnectAsync(string sourceUri, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourceUri))
            {
                Log.Warning("File does not exist: {Path}", sourceUri);
                return Task.FromResult(false);
            }

            _sourceUri = sourceUri;
            _libVlc ??= new LibVLC("--no-xlib", "--no-snapshot-preview", "--no-osd");
            _mediaPlayer ??= new MediaPlayer(_libVlc);

            _media?.Dispose();
            _media = new Media(_libVlc, new Uri(sourceUri));
            _mediaPlayer.Media = _media;

            _mediaPlayer.PositionChanged += (_, args) =>
            {
                if (_mediaPlayer.Length > 0)
                {
                    PositionChanged?.Invoke(this, new SourcePositionChangedEventArgs
                    {
                        Position = TimeSpan.FromMilliseconds(_mediaPlayer.Time),
                        Duration = TimeSpan.FromMilliseconds(_mediaPlayer.Length)
                    });
                }
            };

            _isConnected = true;
            HealthChanged?.Invoke(this, new SourceHealthChangedEventArgs { IsHealthy = true });
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to local file: {Path}", sourceUri);
            _isConnected = false;
            HealthChanged?.Invoke(this, new SourceHealthChangedEventArgs
            {
                IsHealthy = false,
                ErrorMessage = ex.Message
            });
            return Task.FromResult(false);
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _mediaPlayer?.Stop();
        _media?.Dispose();
        _media = null;
        _isConnected = false;
        _isRecording = false;
        HealthChanged?.Invoke(this, new SourceHealthChangedEventArgs { IsHealthy = false });
        return Task.CompletedTask;
    }

    public Task StartPreviewAsync(CancellationToken cancellationToken = default)
    {
        _mediaPlayer?.Play();
        return Task.CompletedTask;
    }

    public Task StopPreviewAsync(CancellationToken cancellationToken = default)
    {
        _mediaPlayer?.Pause();
        return Task.CompletedTask;
    }

    public Task<string> StartRecordingAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        _recordingStartTime = DateTime.Now;
        _recordingStartPosition = CurrentPosition;
        _recordingOutputPath = outputPath;
        _isRecording = true;
        Log.Information("Recording started at position {Position} for {File}", _recordingStartPosition, _sourceUri);
        return Task.FromResult(outputPath);
    }

    public async Task<RecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        _isRecording = false;
        var endPosition = CurrentPosition;
        var endTime = DateTime.Now;

        var ffmpegPath = _settingsService.Settings.FFmpegPath;
        if (!Path.IsPathRooted(ffmpegPath))
            ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ffmpegPath);
        var startSec = _recordingStartPosition.TotalSeconds;
        var endSec = endPosition.TotalSeconds;

        Log.Information("Extracting segment from {Start}s to {End}s", startSec, endSec);

        try
        {
            var result = await Cli.Wrap(ffmpegPath)
                .WithArguments(args => args
                    .Add("-y")
                    .Add("-ss").Add(startSec.ToString("F3"))
                    .Add("-to").Add(endSec.ToString("F3"))
                    .Add("-i").Add(_sourceUri)
                    .Add("-c").Add("copy")
                    .Add(_recordingOutputPath))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            if (result.ExitCode != 0)
            {
                Log.Warning("FFmpeg exited with code {Code}: {Stderr}", result.ExitCode, result.StandardError);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FFmpeg extraction failed");
        }

        var fileInfo = new FileInfo(_recordingOutputPath);
        return new RecordingResult
        {
            FilePath = _recordingOutputPath,
            DurationSeconds = (endSec - startSec),
            StartTime = _recordingStartTime,
            EndTime = endTime,
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mediaPlayer?.Dispose();
        _media?.Dispose();
        _libVlc?.Dispose();
    }
}
