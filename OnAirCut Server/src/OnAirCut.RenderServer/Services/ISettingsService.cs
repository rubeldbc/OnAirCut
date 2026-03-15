using OnAirCut.RenderServer.Models;

namespace OnAirCut.RenderServer.Services;

public interface ISettingsService
{
    RenderServerSettings Settings { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    event EventHandler? SettingsChanged;
}
