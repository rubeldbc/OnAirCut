using System.IO;
using System.Text.Json;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class AdSetProviderService : IAdSetProvider, IDisposable
{
    private readonly ISharedFolderService _sharedFolderService;
    private FileSystemWatcher? _watcher;
    private List<AdSetConfig> _adSets = [];
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AdSetProviderService(ISharedFolderService sharedFolderService)
    {
        _sharedFolderService = sharedFolderService;
    }

    public event EventHandler? AdSetsChanged;

    public void StartWatching()
    {
        var adSetsPath = _sharedFolderService.GetFullPath(FolderNames.AssetsAdSets);

        if (!Directory.Exists(adSetsPath))
        {
            Log.Warning("Ad sets folder does not exist: {Path}", adSetsPath);
            return;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(adSetsPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        _watcher.Created += (_, _) => RefreshAdSets();
        _watcher.Changed += (_, _) => RefreshAdSets();
        _watcher.Deleted += (_, _) => RefreshAdSets();
        _watcher.Renamed += (_, _) => RefreshAdSets();
        _watcher.EnableRaisingEvents = true;

        RefreshAdSets();
    }

    private void RefreshAdSets()
    {
        try
        {
            var adSetsPath = _sharedFolderService.GetFullPath(FolderNames.AssetsAdSets);
            var newAdSets = new List<AdSetConfig>();

            if (Directory.Exists(adSetsPath))
            {
                foreach (var dir in Directory.GetDirectories(adSetsPath))
                {
                    var configFile = Path.Combine(dir, "config.json");
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            var json = File.ReadAllText(configFile);
                            var config = JsonSerializer.Deserialize<AdSetConfig>(json, JsonOptions);
                            if (config is not null)
                            {
                                if (string.IsNullOrEmpty(config.Name))
                                    config.Name = Path.GetFileName(dir);
                                newAdSets.Add(config);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to read ad set config from {Path}", configFile);
                        }
                    }
                }
            }

            _adSets = newAdSets;
            AdSetsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh ad sets");
        }
    }

    public Task<IReadOnlyList<AdSetConfig>> GetAvailableAdSetsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AdSetConfig>>(_adSets.AsReadOnly());
    }

    public Task<AdSetConfig?> GetAdSetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var adSet = _adSets.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(adSet);
    }

    public string GetAdSetFolderPath(string adSetName)
    {
        return _sharedFolderService.GetFullPath(Path.Combine(FolderNames.AssetsAdSets, adSetName));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
    }
}
