using System.Text.Json.Serialization;
using OnAirCut.Core.Enums;

namespace OnAirCut.Recorder.Models;

public class RecorderSettings
{
    [JsonPropertyName("sharedFolderPath")]
    public string SharedFolderPath { get; set; } = string.Empty;

    [JsonPropertyName("defaultSourceType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SourceType DefaultSourceType { get; set; } = SourceType.LocalFile;

    [JsonPropertyName("defaultAdSet")]
    public string DefaultAdSet { get; set; } = string.Empty;

    [JsonPropertyName("recordingFormat")]
    public string RecordingFormat { get; set; } = "mp4";

    [JsonPropertyName("recordingCodec")]
    public string RecordingCodec { get; set; } = "libx264";

    [JsonPropertyName("audioDevice")]
    public string AudioDevice { get; set; } = string.Empty;

    [JsonPropertyName("monitorVolume")]
    public int MonitorVolume { get; set; } = 70;

    [JsonPropertyName("operatorName")]
    public string OperatorName { get; set; } = Environment.UserName;

    [JsonPropertyName("ffmpegPath")]
    public string FFmpegPath { get; set; } = "lib/ffmpeg/ffmpeg.exe";

    [JsonPropertyName("ytDlpPath")]
    public string YtDlpPath { get; set; } = "lib/yt-dlp/yt-dlp.exe";

    [JsonPropertyName("autoSubmitOnRecordStop")]
    public bool AutoSubmitOnRecordStop { get; set; }

    [JsonPropertyName("renderServerApiUrl")]
    public string RenderServerApiUrl { get; set; } = "http://localhost:5123";

    [JsonPropertyName("lastSourceUri")]
    public string LastSourceUri { get; set; } = string.Empty;

    [JsonPropertyName("lastSourceDevice")]
    public string LastSourceDevice { get; set; } = string.Empty;

    [JsonPropertyName("ocrProfileName")]
    public string OcrProfileName { get; set; } = string.Empty;

    [JsonPropertyName("ocrCropX")]
    public int OcrCropX { get; set; }

    [JsonPropertyName("ocrCropY")]
    public int OcrCropY { get; set; }

    [JsonPropertyName("ocrCropWidth")]
    public int OcrCropWidth { get; set; }

    [JsonPropertyName("ocrCropHeight")]
    public int OcrCropHeight { get; set; }

    [JsonPropertyName("adPanelWidth")]
    public double AdPanelWidth { get; set; } = 300;

    public RecorderSettings Clone()
    {
        return new RecorderSettings
        {
            SharedFolderPath = SharedFolderPath,
            DefaultSourceType = DefaultSourceType,
            DefaultAdSet = DefaultAdSet,
            RecordingFormat = RecordingFormat,
            RecordingCodec = RecordingCodec,
            AudioDevice = AudioDevice,
            MonitorVolume = MonitorVolume,
            OperatorName = OperatorName,
            FFmpegPath = FFmpegPath,
            YtDlpPath = YtDlpPath,
            AutoSubmitOnRecordStop = AutoSubmitOnRecordStop,
            RenderServerApiUrl = RenderServerApiUrl,
            LastSourceUri = LastSourceUri,
            LastSourceDevice = LastSourceDevice,
            OcrProfileName = OcrProfileName,
            OcrCropX = OcrCropX,
            OcrCropY = OcrCropY,
            OcrCropWidth = OcrCropWidth,
            OcrCropHeight = OcrCropHeight,
            AdPanelWidth = AdPanelWidth,
        };
    }
}
