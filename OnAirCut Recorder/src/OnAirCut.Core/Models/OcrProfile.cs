using System.Text.Json.Serialization;
using OnAirCut.Core.Enums;

namespace OnAirCut.Core.Models;

public class OcrProfile
{
    [JsonPropertyName("profileName")]
    public string ProfileName { get; set; } = string.Empty;

    [JsonPropertyName("sourceName")]
    public string SourceName { get; set; } = string.Empty;

    [JsonPropertyName("cropX")]
    public int CropX { get; set; }

    [JsonPropertyName("cropY")]
    public int CropY { get; set; }

    [JsonPropertyName("cropWidth")]
    public int CropWidth { get; set; }

    [JsonPropertyName("cropHeight")]
    public int CropHeight { get; set; }

    [JsonPropertyName("resizeScale")]
    public double ResizeScale { get; set; } = 2.0;

    [JsonPropertyName("thresholdMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ThresholdMode ThresholdMode { get; set; } = ThresholdMode.None;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}
