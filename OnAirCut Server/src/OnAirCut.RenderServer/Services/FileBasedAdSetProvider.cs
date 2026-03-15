using System.IO;
using System.Text.Json;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class FileBasedAdSetProvider : IAdSetProvider
{
    private readonly ISharedFolderService _sharedFolderService;

    public FileBasedAdSetProvider(ISharedFolderService sharedFolderService)
    {
        _sharedFolderService = sharedFolderService;
    }

    public event EventHandler? AdSetsChanged
    {
        add { }
        remove { }
    }

    public async Task<IReadOnlyList<AdSetConfig>> GetAvailableAdSetsAsync(CancellationToken cancellationToken = default)
    {
        var adSetsPath = _sharedFolderService.GetFullPath(FolderNames.AssetsAdSets);
        var result = new List<AdSetConfig>();

        if (!Directory.Exists(adSetsPath))
            return result;

        foreach (var dir in Directory.GetDirectories(adSetsPath))
        {
            var configPath = Path.Combine(dir, "config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(configPath, cancellationToken);
                    var config = JsonSerializer.Deserialize<AdSetConfig>(json);
                    if (config != null)
                    {
                        // Resolve relative paths to absolute
                        if (!string.IsNullOrEmpty(config.TvcFile) && !Path.IsPathRooted(config.TvcFile))
                            config.TvcFile = Path.Combine(dir, config.TvcFile);
                        if (!string.IsNullOrEmpty(config.OverlayFile) && !Path.IsPathRooted(config.OverlayFile))
                            config.OverlayFile = Path.Combine(dir, config.OverlayFile);

                        result.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to read ad set config: {Path}", configPath);
                }
            }
        }

        return result;
    }

    public async Task<AdSetConfig?> GetAdSetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var adSets = await GetAvailableAdSetsAsync(cancellationToken);
        return adSets.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public string GetAdSetFolderPath(string adSetName)
    {
        return Path.Combine(_sharedFolderService.GetFullPath(FolderNames.AssetsAdSets), adSetName);
    }
}
