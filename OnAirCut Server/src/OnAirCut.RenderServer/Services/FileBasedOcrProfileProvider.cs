using System.IO;
using System.Text.Json;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class FileBasedOcrProfileProvider : IOcrProfileProvider
{
    private readonly ISharedFolderService _sharedFolderService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FileBasedOcrProfileProvider(ISharedFolderService sharedFolderService)
    {
        _sharedFolderService = sharedFolderService;
    }

    public event EventHandler? ProfilesChanged;

    public async Task<IReadOnlyList<OcrProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profilesPath = _sharedFolderService.GetFullPath(FolderNames.AssetsOcrProfiles);
        var result = new List<OcrProfile>();

        if (!Directory.Exists(profilesPath))
            return result;

        foreach (var file in Directory.GetFiles(profilesPath, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var profile = JsonSerializer.Deserialize<OcrProfile>(json);
                if (profile != null)
                    result.Add(profile);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read OCR profile: {Path}", file);
            }
        }

        return result;
    }

    public async Task<OcrProfile?> GetProfileByNameAsync(string profileName, CancellationToken cancellationToken = default)
    {
        var profiles = await GetProfilesAsync(cancellationToken);
        return profiles.FirstOrDefault(p => p.ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<OcrProfile?> GetDefaultProfileAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await GetProfilesAsync(cancellationToken);
        return profiles.FirstOrDefault(p => p.IsActive) ?? profiles.FirstOrDefault();
    }

    public async Task SaveProfileAsync(OcrProfile profile, CancellationToken cancellationToken = default)
    {
        var profilesPath = _sharedFolderService.GetFullPath(FolderNames.AssetsOcrProfiles);
        Directory.CreateDirectory(profilesPath);

        var filePath = Path.Combine(profilesPath, $"{profile.ProfileName}.json");
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task DeleteProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        var profilesPath = _sharedFolderService.GetFullPath(FolderNames.AssetsOcrProfiles);
        var filePath = Path.Combine(profilesPath, $"{profileName}.json");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }
}
