using System.Text.Json.Serialization;
using OnAirCut.Core.Enums;

namespace OnAirCut.Core.Models;

public class AdSetConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tvcFile")]
    public string? TvcFile { get; set; }

    [JsonPropertyName("overlayFile")]
    public string? OverlayFile { get; set; }

    [JsonPropertyName("insertMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InsertMode InsertMode { get; set; } = InsertMode.None;

    [JsonPropertyName("insertAtSec")]
    public double InsertAtSec { get; set; }

    [JsonPropertyName("overlayStartSec")]
    public double OverlayStartSec { get; set; }

    [JsonPropertyName("overlayEndSec")]
    public double OverlayEndSec { get; set; } = 9999;

    [JsonPropertyName("outputWidth")]
    public int OutputWidth { get; set; } = 1920;

    [JsonPropertyName("outputHeight")]
    public int OutputHeight { get; set; } = 1080;
}
