using System.IO;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Utilities;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class SharedFolderHealthService : ISharedFolderService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private Timer? _healthCheckTimer;
    private bool _isHealthy;
    private string? _lastError;
    private bool _structureInitialized;

    public SharedFolderHealthService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string SharedFolderPath => _settingsService.Settings.SharedFolderPath;
    public bool IsHealthy => _isHealthy;
    public string? LastError => _lastError;

    public event EventHandler<SharedFolderHealthChangedEventArgs>? HealthChanged;

    public void StartMonitoring()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = new Timer(OnHealthCheck, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private async void OnHealthCheck(object? state)
    {
        try
        {
            var wasHealthy = _isHealthy;
            var result = await ValidateAsync();

            if (result && !_structureInitialized)
            {
                try
                {
                    await InitializeStructureAsync();
                    _structureInitialized = true;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to initialize shared folder structure");
                }
            }

            if (wasHealthy != _isHealthy)
            {
                HealthChanged?.Invoke(this, new SharedFolderHealthChangedEventArgs
                {
                    IsHealthy = _isHealthy,
                    ErrorMessage = _lastError
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Health check failed unexpectedly");
        }
    }

    public Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var path = SharedFolderPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                _isHealthy = false;
                _lastError = "Shared folder path not configured";
                return false;
            }

            var issues = SharedFolderValidator.Validate(path);
            if (issues.Count == 0)
            {
                _isHealthy = true;
                _lastError = null;
                return true;
            }

            _isHealthy = false;
            _lastError = string.Join("; ", issues);
            return false;
        }, cancellationToken);
    }

    public Task InitializeStructureAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            SharedFolderInitializer.Initialize(SharedFolderPath);
        }, cancellationToken);
    }

    public string GetFullPath(string relativePath)
    {
        return Path.Combine(SharedFolderPath, relativePath);
    }

    public string GetDateSubfolder(string parentRelativePath, DateTime date)
    {
        return SharedFolderInitializer.EnsureDateSubfolder(SharedFolderPath, parentRelativePath, date);
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
    }
}
