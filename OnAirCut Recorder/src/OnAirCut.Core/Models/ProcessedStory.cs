using OnAirCut.Core.Enums;

namespace OnAirCut.Core.Models;

public class ProcessedStory
{
    public int Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string? TitleRaw { get; set; }
    public string? TitleNormalized { get; set; }
    public string? SafeFolderName { get; set; }
    public SourceType SourceType { get; set; }
    public string? SourceName { get; set; }
    public DateTime OnAirDateTime { get; set; }
    public DateTime? ClipStartTime { get; set; }
    public DateTime? ClipEndTime { get; set; }
    public double DurationSeconds { get; set; }
    public string? AdSetName { get; set; }
    public string? OverlaySetName { get; set; }
    public string RawClipPath { get; set; } = string.Empty;
    public string? OutputFolderPath { get; set; }
    public string? OutputVideoPath { get; set; }
    public string? FramesPath { get; set; }
    public double? OcrConfidence { get; set; }
    public string? OcrProfileUsed { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AdSetConfigJson { get; set; }
}
