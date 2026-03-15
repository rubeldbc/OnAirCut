using OnAirCut.Recorder.Models;

namespace OnAirCut.Recorder.Services;

public interface ISettingsService
{
    RecorderSettings Settings { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    void UpdateSettings(Action<RecorderSettings> updateAction);
    event EventHandler? SettingsChanged;
}
