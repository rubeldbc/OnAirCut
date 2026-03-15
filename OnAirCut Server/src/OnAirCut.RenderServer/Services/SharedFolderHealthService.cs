using System.IO;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Utilities;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class SharedFolderHealthService : ISharedFolderService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private Timer? _healthCheckTimer;
    private bool _isHealthy;
    private string? _lastError;

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
        _healthCheckTimer = new Timer(OnHealthCheck, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void OnHealthCheck(object? state)
    {
        try
        {
            var issues = SharedFolderValidator.Validate(SharedFolderPath);
            var wasHealthy = _isHealthy;
            _isHealthy = issues.Count == 0;
            _lastError = _isHealthy ? null : string.Join("; ", issues);

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
            var wasHealthy = _isHealthy;
            _isHealthy = false;
            _lastError = ex.Message;

            if (wasHealthy)
            {
                HealthChanged?.Invoke(this, new SharedFolderHealthChangedEventArgs
                {
                    IsHealthy = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }

    public Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var issues = SharedFolderValidator.Validate(SharedFolderPath);
        _isHealthy = issues.Count == 0;
        _lastError = _isHealthy ? null : string.Join("; ", issues);
        return Task.FromResult(_isHealthy);
    }

    public Task InitializeStructureAsync(CancellationToken cancellationToken = default)
    {
        SharedFolderInitializer.Initialize(SharedFolderPath);
        return Task.CompletedTask;
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
    }
}
