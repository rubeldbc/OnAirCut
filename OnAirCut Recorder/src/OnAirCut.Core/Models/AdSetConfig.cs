using System.Text.Json.Serialization;

namespace OnAirCut.Core.Models;

public class AdSetConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("outputWidth")]
    public int OutputWidth { get; set; } = 1920;

    [JsonPropertyName("outputHeight")]
    public int OutputHeight { get; set; } = 1080;

    [JsonPropertyName("doggy")]
    public DoggyAdConfig? Doggy { get; set; }

    [JsonPropertyName("popup")]
    public PopupAdConfig? Popup { get; set; }

    [JsonPropertyName("tvc")]
    public TvcAdConfig? Tvc { get; set; }

    /// <summary>
    /// Returns true if at least one ad type is enabled.
    /// </summary>
    [JsonIgnore]
    public bool HasAnyEnabled =>
        (Doggy?.Enabled ?? false) ||
        (Popup?.Enabled ?? false) ||
        (Tvc?.Enabled ?? false);
}
