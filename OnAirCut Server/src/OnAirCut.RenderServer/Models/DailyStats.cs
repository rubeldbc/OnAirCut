namespace OnAirCut.RenderServer.Models;

public class DailyStats
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalCount => PendingCount + ProcessingCount + CompletedCount + FailedCount;
    public double AverageRenderTimeSeconds { get; set; }
    public double TotalOutputDurationSeconds { get; set; }
    public double OcrSuccessRate { get; set; }
}
