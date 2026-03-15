using System.Text.Json.Serialization;
using OnAirCut.Core.Enums;

namespace OnAirCut.Core.Models;

public class JobFile
{
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("rawClipPath")]
    public string RawClipPath { get; set; } = string.Empty;

    [JsonPropertyName("sourceType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SourceType SourceType { get; set; }

    [JsonPropertyName("sourceName")]
    public string SourceName { get; set; } = string.Empty;

    [JsonPropertyName("clipStartTime")]
    public DateTime ClipStartTime { get; set; }

    [JsonPropertyName("clipEndTime")]
    public DateTime ClipEndTime { get; set; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("adSetName")]
    public string? AdSetName { get; set; }

    [JsonPropertyName("overlaySetName")]
    public string? OverlaySetName { get; set; }

    [JsonPropertyName("ocrProfileName")]
    public string? OcrProfileName { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("submittedBy")]
    public string SubmittedBy { get; set; } = string.Empty;

    [JsonPropertyName("submittedAt")]
    public DateTime SubmittedAt { get; set; }
}
