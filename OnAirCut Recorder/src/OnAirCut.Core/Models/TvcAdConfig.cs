using System.Text.Json.Serialization;

namespace OnAirCut.Core.Models;

public class TvcAdConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}
