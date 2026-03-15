namespace OnAirCut.Core.Interfaces;

public interface ISharedFolderService
{
    string SharedFolderPath { get; }
    bool IsHealthy { get; }
    string? LastError { get; }

    Task<bool> ValidateAsync(CancellationToken cancellationToken = default);
    Task InitializeStructureAsync(CancellationToken cancellationToken = default);
    string GetFullPath(string relativePath);
    string GetDateSubfolder(string parentRelativePath, DateTime date);

    event EventHandler<SharedFolderHealthChangedEventArgs>? HealthChanged;
}

public class SharedFolderHealthChangedEventArgs : EventArgs
{
    public bool IsHealthy { get; init; }
    public string? ErrorMessage { get; init; }
}
