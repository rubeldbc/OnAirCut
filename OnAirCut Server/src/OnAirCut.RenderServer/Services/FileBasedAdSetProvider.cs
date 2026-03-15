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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".avi", ".mkv"];

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
            var folderName = Path.GetFileName(dir);
            var configPath = Path.Combine(dir, "config.json");

            if (File.Exists(configPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(configPath, cancellationToken);
                    var config = JsonSerializer.Deserialize<AdSetConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        if (string.IsNullOrEmpty(config.Name))
                            config.Name = folderName;
                        ResolveFilePaths(config, dir);
                        result.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to read ad set config: {Path}", configPath);
                }
            }
            else
            {
                result.Add(new AdSetConfig { Name = folderName });
            }
        }

        return result;
    }

    public async Task<AdSetConfig?> GetAdSetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var adSets = await GetAvailableAdSetsAsync(cancellationToken);
        return adSets.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAdSetConfigAsync(string adSetName, AdSetConfig config, CancellationToken cancellationToken = default)
    {
        var folderPath = GetAdSetFolderPath(adSetName);
        Directory.CreateDirectory(folderPath);

        var configPath = Path.Combine(folderPath, "config.json");
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, cancellationToken);

        Log.Information("Saved ad set config: {Name}", adSetName);
    }

    public Task<IReadOnlyList<string>> GetAvailableFilesAsync(string adSetName, CancellationToken cancellationToken = default)
    {
        var folderPath = GetAdSetFolderPath(adSetName);
        if (!Directory.Exists(folderPath))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var files = Directory.GetFiles(folderPath)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .Cast<string>()
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public Task<IReadOnlyList<string>> GetAllAdSetFolderNamesAsync(CancellationToken cancellationToken = default)
    {
        var adSetsPath = _sharedFolderService.GetFullPath(FolderNames.AssetsAdSets);
        if (!Directory.Exists(adSetsPath))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var names = Directory.GetDirectories(adSetsPath)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public string GetAdSetFolderPath(string adSetName)
    {
        return Path.Combine(_sharedFolderService.GetFullPath(FolderNames.AssetsAdSets), adSetName);
    }

    private static void ResolveFilePaths(AdSetConfig config, string adSetDir)
    {
        if (config.Doggy is { File: not null } doggy && !Path.IsPathRooted(doggy.File))
            doggy.File = Path.Combine(adSetDir, doggy.File);
        if (config.Popup is { File: not null } popup && !Path.IsPathRooted(popup.File))
            popup.File = Path.Combine(adSetDir, popup.File);
        if (config.Tvc is { File: not null } tvc && !Path.IsPathRooted(tvc.File))
            tvc.File = Path.Combine(adSetDir, tvc.File);
    }
}
