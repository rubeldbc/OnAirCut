using System.IO;
using CliWrap;
using CliWrap.Buffered;
using LibVLCSharp.Shared;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class LiveFeedSource : IVideoSource
{
    private readonly ISettingsService _settingsService;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private string _deviceName = string.Empty;
    private bool _isConnected;
    private bool _isRecording;
    private DateTime _recordingStartTime;
    private string _recordingOutputPath = string.Empty;
    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private bool _disposed;

    public LiveFeedSource(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public SourceType SourceType => SourceType.LiveFeed;
    public string SourceName => _deviceName;
    public bool IsConnected => _isConnected;
    public bool IsRecording => _isRecording;
    public bool IsHealthy => _isConnected;
    public TimeSpan CurrentPosition => TimeSpan.Zero;
    public TimeSpan? Duration => null;

    public event EventHandler<SourceHealthChangedEventArgs>? HealthChanged;
#pragma warning disable CS0067 // Event is required by IVideoSource interface
    public event EventHandler<SourcePositionChangedEventArgs>? PositionChanged;
#pragma warning restore CS0067

    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public static List<string> EnumerateCaptureDevices()
    {
        var devices = new List<string>();
        try
        {
            using var libVlc = new LibVLC("--no-xlib", "--no-snapshot-preview", "--no-osd");
            using var md = new MediaDiscoverer(libVlc, "dshow");
            // DShow device names can be obtained via FFmpeg or manually
            // For now return a placeholder list
            devices.Add("Default Capture Device");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate capture devices");
        }
        return devices;
    }

    public Task<bool> ConnectAsync(string sourceUri, CancellationToken cancellationToken = default)
    {
        try
        {
            _deviceName = sourceUri;
            _libVlc ??= new LibVLC("--no-xlib", "--no-snapshot-preview", "--no-osd");
            _mediaPlayer ??= new MediaPlayer(_libVlc);

            _media?.Dispose();
            _media = new Media(_libVlc, $"dshow://", FromType.FromLocation);
            _media.AddOption($":dshow-vdev={sourceUri}");
            _media.AddOption(":live-caching=300");
            _mediaPlayer.Media = _media;

            _isConnected = true;
            HealthChanged?.Invoke(this, new SourceHealthChangedEventArgs { IsHealthy = true });
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to live feed: {Device}", sourceUri);
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
        _recordingCts?.Cancel();
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

    private System.Diagnostics.Process? _ffmpegProcess;
    private string _tempTsPath = string.Empty;

    public Task<string> StartRecordingAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        _recordingStartTime = DateTime.Now;
        _recordingOutputPath = outputPath;
        _isRecording = true;

        var ffmpegPath = _settingsService.Settings.FFmpegPath;
        if (!Path.IsPathRooted(ffmpegPath))
            ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ffmpegPath);

        _tempTsPath = Path.ChangeExtension(outputPath, ".ts");

        _ffmpegProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -f dshow -i \"video={_deviceName}\" -c:v {_settingsService.Settings.RecordingCodec} -f mpegts \"{_tempTsPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        _ffmpegProcess.Start();
        _ffmpegProcess.BeginErrorReadLine();

        Log.Information("Live feed recording started to {Path}", _tempTsPath);
        return Task.FromResult(outputPath);
    }

    public async Task<RecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        _isRecording = false;
        var endTime = DateTime.Now;

        if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
        {
            try
            {
                await _ffmpegProcess.StandardInput.WriteAsync('q');
                await _ffmpegProcess.StandardInput.FlushAsync();
                var exited = await Task.Run(() => _ffmpegProcess.WaitForExit(10000));
                if (!exited)
                {
                    _ffmpegProcess.Kill();
                    _ffmpegProcess.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error stopping FFmpeg");
                try { _ffmpegProcess.Kill(); } catch { }
            }
            finally
            {
                _ffmpegProcess.Dispose();
                _ffmpegProcess = null;
            }
        }

        await Task.Delay(300, cancellationToken);

        // Remux .ts to .mp4
        if (File.Exists(_tempTsPath))
        {
            try
            {
                var ffmpegPath = _settingsService.Settings.FFmpegPath;
                if (!Path.IsPathRooted(ffmpegPath))
                    ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ffmpegPath);

                var remux = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-y -i \"{_tempTsPath}\" -c copy -movflags +faststart \"{_recordingOutputPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                remux.Start();
                remux.WaitForExit(30000);
                File.Delete(_tempTsPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Remux failed");
                if (!File.Exists(_recordingOutputPath) && File.Exists(_tempTsPath))
                    File.Move(_tempTsPath, _recordingOutputPath);
            }
        }

        var fileInfo = new FileInfo(_recordingOutputPath);
        return new RecordingResult
        {
            FilePath = _recordingOutputPath,
            DurationSeconds = (endTime - _recordingStartTime).TotalSeconds,
            StartTime = _recordingStartTime,
            EndTime = endTime,
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _recordingCts?.Cancel();
        _recordingCts?.Dispose();
        _mediaPlayer?.Dispose();
        _media?.Dispose();
        _libVlc?.Dispose();
    }
}
