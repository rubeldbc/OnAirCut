namespace OnAirCut.Core.Models;

public class RecordingResult
{
    public string FilePath { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long FileSizeBytes { get; set; }
}
