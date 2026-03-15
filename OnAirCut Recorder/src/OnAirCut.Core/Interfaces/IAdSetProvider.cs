using OnAirCut.Core.Models;

namespace OnAirCut.Core.Interfaces;

public interface IAdSetProvider
{
    Task<IReadOnlyList<AdSetConfig>> GetAvailableAdSetsAsync(CancellationToken cancellationToken = default);
    Task<AdSetConfig?> GetAdSetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task SaveAdSetConfigAsync(string adSetName, AdSetConfig config, CancellationToken cancellationToken = default);
    string GetAdSetFolderPath(string adSetName);

    /// <summary>
    /// Returns video files (mp4, mov) in the ad set folder for file selection UI.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableFilesAsync(string adSetName, CancellationToken cancellationToken = default);

    event EventHandler? AdSetsChanged;
}
