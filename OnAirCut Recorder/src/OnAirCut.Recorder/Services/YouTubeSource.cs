using System.IO;
using CliWrap;
using CliWrap.Buffered;
using LibVLCSharp.Shared;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class YouTubeSource : IVideoSource
{
    private readonly ISettingsService _settingsService;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private string _youtubeUrl = string.Empty;
    private string _resolvedStreamUrl = string.Empty;
    private bool _isConnected;
    private bool _isRecording;
    private DateTime _recordingStartTime;
    private string _recordingOutputPath = string.Empty;
    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private bool _disposed;

    public YouTubeSource(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public SourceType SourceType => SourceType.YouTubeUrl;
    public string SourceName => _youtubeUrl;
    public bool IsConnected => _isConnected;
    public bool IsRecording => _isRecording;
    public bool IsHealthy => _isConnected;
    public TimeSpan CurrentPosition => _mediaPlayer is not null
        ? TimeSpan.FromMilliseconds(_mediaPlayer.Time)
        : TimeSpan.Zero;
    public TimeSpan? Duration => null; // Live streams have no duration

    public event EventHandler<SourceHealthChangedEventArgs>? HealthChanged;
    public event EventHandler<SourcePositionChangedEventArgs>? PositionChanged;

    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public async Task<bool> ConnectAsync(string sourceUri, CancellationToken cancellationToken = default)
    {
        try
        {
            _youtubeUrl = sourceUri;
            Log.Information("Connecting to YouTube: {Url}", sourceUri);

            // Try yt-dlp first to resolve direct stream URL
            var ytDlpPath = ResolveYtDlpPath();
            bool resolved = false;

            if (File.Exists(ytDlpPath))
            {
                try
                {
                    Log.Information("Resolving via yt-dlp: {Path}", ytDlpPath);
                    var result = await Cli.Wrap(ytDlpPath)
                        .WithArguments(args => args
                            .Add("-g")
                            .Add("-f").Add("b")
                            .Add("--no-playlist")
                            .Add("--no-warnings")
                            .Add(sourceUri))
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync(cancellationToken);

                    if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
                    {
                        _resolvedStreamUrl = result.StandardOutput.Trim().Split('\n')[0].Trim();
                        resolved = true;
                        Log.Information("yt-dlp resolved stream URL successfully");
                    }
                    else
                    {
                        Log.Warning("yt-dlp failed (exit {Code}): {Err}", result.ExitCode, result.StandardError);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "yt-dlp execution failed, falling back to direct VLC");
                }
            }
            else
            {
                Log.Information("yt-dlp not found at {Path}, using LibVLC directly", ytDlpPath);
            }

            // Initialize LibVLC
            _libVlc ??= new LibVLC(
                "--no-xlib",
                "--no-snapshot-preview",
                "--no-osd",
                "--network-caching=3000",
                "--live-caching=3000"
            );

            _mediaPlayer?.Dispose();
            _mediaPlayer = new MediaPlayer(_libVlc);
            _mediaPlayer.EndReached += (_, _) =>
            {
                HealthChanged?.Invoke(this, new SourceHealthChangedEventArgs
                {
                    IsHealthy = false,
                    ErrorMessage = "Stream ended"
                });
            };

            // Create media from resolved URL or original YouTube URL
            _media?.Dispose();
            if (resolved)
            {
                _media = new Media(_libVlc, new Uri(_resolvedStreamUrl));
            }
            else
            {
                // LibVLC can handle YouTube URLs directly (with its built-in lua scripts)
                _media = new Media(_libVlc, new Uri(NormalizeYouTubeUrl(sourceUri)));
            }

            _mediaPlayer.Media = _media;
            _isConnected = true;

            HealthChanged?.Invoke(this, new SourceHealthChangedEventArgs { IsHealthy = true });

            // Auto-start playback
            _mediaPlayer.Play();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect YouTube: {Url}", sourceUri);
            _isConnected = false;
            HealthChanged?.Invoke(this, new SourceHealthChangedEventArgs
            {
                IsHealthy = false,
                ErrorMessage = ex.Message
            });
            return false;
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
        _resolvedStreamUrl = string.Empty;
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

        var ffmpegPath = ResolveFFmpegPath();
        var streamUrl = !string.IsNullOrEmpty(_resolvedStreamUrl) ? _resolvedStreamUrl : _youtubeUrl;

        // Record to .ts container first (MPEG-TS doesn't need moov atom, so it's always valid even if killed)
        _tempTsPath = Path.ChangeExtension(outputPath, ".ts");

        _ffmpegProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -i \"{streamUrl}\" -c copy -f mpegts \"{_tempTsPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        _ffmpegProcess.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Log.Debug("FFmpeg: {Data}", e.Data);
        };

        _ffmpegProcess.Start();
        _ffmpegProcess.BeginErrorReadLine();

        Log.Information("YouTube recording started to {Path}", _tempTsPath);
        return Task.FromResult(outputPath);
    }

    public async Task<RecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        _isRecording = false;
        var endTime = DateTime.Now;

        // Send 'q' to FFmpeg stdin for graceful stop
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
                Log.Information("FFmpeg recording stopped, exit code: {Code}", _ffmpegProcess.ExitCode);
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

        // Remux .ts to .mp4 for proper playback
        if (File.Exists(_tempTsPath))
        {
            try
            {
                var ffmpegPath = ResolveFFmpegPath();
                var remux = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-y -i \"{_tempTsPath}\" -c copy -movflags +faststart \"{_recordingOutputPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    }
                };
                remux.Start();
                remux.WaitForExit(30000);
                Log.Information("Remuxed .ts to .mp4 successfully");

                // Clean up temp .ts file
                File.Delete(_tempTsPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Remux failed, keeping .ts file");
                // If remux fails, rename .ts to .mp4 output path
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

    private string ResolveYtDlpPath()
    {
        var settingsPath = _settingsService.Settings.YtDlpPath;
        if (!string.IsNullOrEmpty(settingsPath))
        {
            // If relative, resolve from app base directory
            if (!Path.IsPathRooted(settingsPath))
                return Path.Combine(AppPaths.BaseDirectory, settingsPath);
            return settingsPath;
        }
        return Path.Combine(AppPaths.LibDirectory, "yt-dlp", "yt-dlp.exe");
    }

    private string ResolveFFmpegPath()
    {
        var settingsPath = _settingsService.Settings.FFmpegPath;
        if (!string.IsNullOrEmpty(settingsPath))
        {
            if (!Path.IsPathRooted(settingsPath))
                return Path.Combine(AppPaths.BaseDirectory, settingsPath);
            return settingsPath;
        }
        return AppPaths.FFmpegPath;
    }

    private static string NormalizeYouTubeUrl(string url)
    {
        // Convert youtu.be short URLs to full URLs
        if (url.Contains("youtu.be/"))
        {
            var videoId = url.Split("youtu.be/").Last().Split('?').First();
            return $"https://www.youtube.com/watch?v={videoId}";
        }
        return url;
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
