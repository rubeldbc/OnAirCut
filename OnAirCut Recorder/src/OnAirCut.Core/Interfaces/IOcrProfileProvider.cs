using OnAirCut.Core.Models;

namespace OnAirCut.Core.Interfaces;

public interface IOcrProfileProvider
{
    Task<IReadOnlyList<OcrProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<OcrProfile?> GetProfileByNameAsync(string profileName, CancellationToken cancellationToken = default);
    Task<OcrProfile?> GetDefaultProfileAsync(CancellationToken cancellationToken = default);
    Task SaveProfileAsync(OcrProfile profile, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(string profileName, CancellationToken cancellationToken = default);
    event EventHandler? ProfilesChanged;
}
