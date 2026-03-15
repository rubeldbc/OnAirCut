using System.Text.Json.Serialization;

namespace OnAirCut.RenderServer.Models;

public class RenderServerSettings
{
    [JsonPropertyName("sharedFolderPath")]
    public string SharedFolderPath { get; set; } = string.Empty;

    [JsonPropertyName("ffmpegPath")]
    public string FFmpegPath { get; set; } = "lib/ffmpeg/ffmpeg.exe";

    [JsonPropertyName("ffprobePath")]
    public string FFprobePath { get; set; } = "lib/ffmpeg/ffprobe.exe";

    [JsonPropertyName("ocrEnginePath")]
    public string OcrEnginePath { get; set; } = "lib/tessdata";

    [JsonPropertyName("ocrLanguage")]
    public string OcrLanguage { get; set; } = "ben";

    [JsonPropertyName("localDatabasePath")]
    public string LocalDatabasePath { get; set; } = "AppData/onaircut.db";

    [JsonPropertyName("tempWorkingFolder")]
    public string TempWorkingFolder { get; set; } = string.Empty;

    [JsonPropertyName("maxConcurrentRenders")]
    public int MaxConcurrentRenders { get; set; } = 1;

    [JsonPropertyName("jobPollIntervalMs")]
    public int JobPollIntervalMs { get; set; } = 2000;

    [JsonPropertyName("fileReadyCheckIntervalMs")]
    public int FileReadyCheckIntervalMs { get; set; } = 1000;

    [JsonPropertyName("fileReadyStableSeconds")]
    public int FileReadyStableSeconds { get; set; } = 3;

    [JsonPropertyName("outputVideoCodec")]
    public string OutputVideoCodec { get; set; } = "libx264";

    [JsonPropertyName("outputVideoPreset")]
    public string OutputVideoPreset { get; set; } = "fast";

    [JsonPropertyName("outputVideoCRF")]
    public int OutputVideoCRF { get; set; } = 18;

    [JsonPropertyName("outputAudioCodec")]
    public string OutputAudioCodec { get; set; } = "aac";

    [JsonPropertyName("outputAudioBitrate")]
    public string OutputAudioBitrate { get; set; } = "192k";

    [JsonPropertyName("cleanupWorkingFolderAfterDays")]
    public int CleanupWorkingFolderAfterDays { get; set; } = 7;

    [JsonPropertyName("frameExtractionCount")]
    public int FrameExtractionCount { get; set; } = 20;

    [JsonPropertyName("ocrMultiFrameCount")]
    public int OcrMultiFrameCount { get; set; } = 5;

    [JsonPropertyName("apiPort")]
    public int ApiPort { get; set; } = 5123;

    [JsonPropertyName("apiEnabled")]
    public bool ApiEnabled { get; set; } = true;
}
