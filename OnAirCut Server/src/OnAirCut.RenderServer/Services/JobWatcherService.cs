using System.IO;
using System.Threading.Channels;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using OnAirCut.Core.Utilities;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class JobWatcherService : IDisposable
{
    private readonly ISharedFolderService _sharedFolderService;
    private readonly ISettingsService _settingsService;
    private FileSystemWatcher? _watcher;
    private Timer? _pollTimer;
    private CancellationTokenSource? _cts;
    private readonly Channel<JobFile> _jobChannel;
    private bool _isWatching;
    private int _pendingCount;

    public JobWatcherService(ISharedFolderService sharedFolderService, ISettingsService settingsService)
    {
        _sharedFolderService = sharedFolderService;
        _settingsService = settingsService;
        _jobChannel = Channel.CreateUnbounded<JobFile>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        // Auto-start watching when shared folder becomes healthy
        _sharedFolderService.HealthChanged += (_, e) =>
        {
            if (e.IsHealthy && !_isWatching)
            {
                Log.Information("Shared folder became healthy, starting job watcher");
                StartWatching();
            }
        };
    }

    public bool IsWatching => _isWatching;
    public int PendingCount => _pendingCount;
    public ChannelReader<JobFile> JobReader => _jobChannel.Reader;

    public event EventHandler<int>? PendingCountChanged;

    /// <summary>
    /// Enqueue a job directly (from API submission). Bypasses file watcher.
    /// </summary>
    public async Task EnqueueJobAsync(JobFile job)
    {
        await _jobChannel.Writer.WriteAsync(job);
        Log.Information("Job enqueued via API: {JobId}", job.JobId);
    }

    public void StartWatching()
    {
        if (_isWatching) return;
        if (!_sharedFolderService.IsHealthy)
        {
            Log.Warning("Cannot start watching: shared folder not healthy");
            return;
        }

        _cts = new CancellationTokenSource();

        var pendingPath = _sharedFolderService.GetFullPath(FolderNames.JobsPending);
        Directory.CreateDirectory(pendingPath);

        try
        {
            _watcher = new FileSystemWatcher(pendingPath, "*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _watcher.Created += OnFileCreated;
            _watcher.Error += OnWatcherError;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "FileSystemWatcher failed, relying on polling only");
        }

        _pollTimer = new Timer(OnPollTimer, null, TimeSpan.Zero,
            TimeSpan.FromMilliseconds(_settingsService.Settings.JobPollIntervalMs));

        _isWatching = true;
        Log.Information("Job watcher started on {Path}", pendingPath);
    }

    public void StopWatching()
    {
        if (!_isWatching) return;

        _cts?.Cancel();
        _watcher?.Dispose();
        _watcher = null;
        _pollTimer?.Dispose();
        _pollTimer = null;
        _isWatching = false;

        Log.Information("Job watcher stopped");
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        await Task.Delay(500); // Brief delay to let file finish writing
        await ScanPendingFolderAsync();
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Log.Warning(e.GetException(), "FileSystemWatcher error");
    }

    private async void OnPollTimer(object? state)
    {
        await ScanPendingFolderAsync();
    }

    private async Task ScanPendingFolderAsync()
    {
        if (_cts?.IsCancellationRequested == true) return;
        if (!_sharedFolderService.IsHealthy) return;

        try
        {
            var pendingFiles = JobFileHelper.GetPendingJobFiles(_sharedFolderService.SharedFolderPath).ToList();
            _pendingCount = pendingFiles.Count;
            PendingCountChanged?.Invoke(this, _pendingCount);

            foreach (var filePath in pendingFiles)
            {
                if (_cts?.IsCancellationRequested == true) break;

                try
                {
                    var processingFolder = _sharedFolderService.GetFullPath(FolderNames.JobsProcessing);
                    if (!JobFileHelper.TryMoveJob(filePath, processingFolder))
                        continue;

                    var movedPath = Path.Combine(processingFolder, Path.GetFileName(filePath));
                    var job = await JobFileHelper.ReadJobAsync(movedPath, _cts?.Token ?? CancellationToken.None);
                    if (job == null)
                    {
                        Log.Warning("Failed to read job file: {Path}", movedPath);
                        continue;
                    }

                    await JobFileHelper.CreateLockFileAsync(_sharedFolderService.SharedFolderPath, job.JobId,
                        _cts?.Token ?? CancellationToken.None);

                    await _jobChannel.Writer.WriteAsync(job, _cts?.Token ?? CancellationToken.None);
                    Log.Information("Enqueued job {JobId}", job.JobId);

                    _pendingCount = Math.Max(0, _pendingCount - 1);
                    PendingCountChanged?.Invoke(this, _pendingCount);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing pending job file: {Path}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scanning pending folder");
        }
    }

    public void Dispose()
    {
        StopWatching();
        _cts?.Dispose();
    }
}
