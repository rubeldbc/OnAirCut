using OnAirCut.Core.Models;

namespace OnAirCut.Core.Interfaces;

public interface IAdSetProvider
{
    Task<IReadOnlyList<AdSetConfig>> GetAvailableAdSetsAsync(CancellationToken cancellationToken = default);
    Task<AdSetConfig?> GetAdSetByNameAsync(string name, CancellationToken cancellationToken = default);
    string GetAdSetFolderPath(string adSetName);
    event EventHandler? AdSetsChanged;
}
