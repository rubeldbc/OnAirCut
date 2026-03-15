using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;

namespace OnAirCut.Core.Interfaces;

public interface IVideoSource : IDisposable
{
    SourceType SourceType { get; }
    string SourceName { get; }
    bool IsConnected { get; }
    bool IsRecording { get; }
    bool IsHealthy { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan? Duration { get; }

    Task<bool> ConnectAsync(string sourceUri, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task StartPreviewAsync(CancellationToken cancellationToken = default);
    Task StopPreviewAsync(CancellationToken cancellationToken = default);
    Task<string> StartRecordingAsync(string outputPath, CancellationToken cancellationToken = default);
    Task<RecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default);

    event EventHandler<SourceHealthChangedEventArgs>? HealthChanged;
    event EventHandler<SourcePositionChangedEventArgs>? PositionChanged;
}

public class SourceHealthChangedEventArgs : EventArgs
{
    public bool IsHealthy { get; init; }
    public string? ErrorMessage { get; init; }
}

public class SourcePositionChangedEventArgs : EventArgs
{
    public TimeSpan Position { get; init; }
    public TimeSpan? Duration { get; init; }
}
