using System.Text.Json.Serialization;

namespace OnAirCut.Core.Models;

public class DoggyAdConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("startFrom")]
    public double StartFrom { get; set; }

    [JsonPropertyName("positionX")]
    public double PositionX { get; set; }

    [JsonPropertyName("positionY")]
    public double PositionY { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; } = 200;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 150;

    [JsonPropertyName("cropTop")]
    public double CropTop { get; set; }

    [JsonPropertyName("cropRight")]
    public double CropRight { get; set; }

    [JsonPropertyName("cropBottom")]
    public double CropBottom { get; set; }

    [JsonPropertyName("cropLeft")]
    public double CropLeft { get; set; }

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;
}
