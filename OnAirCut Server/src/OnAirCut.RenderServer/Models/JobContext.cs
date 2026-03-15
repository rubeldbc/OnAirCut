using OnAirCut.Core.Models;

namespace OnAirCut.RenderServer.Models;

public class JobContext
{
    public JobFile JobFile { get; set; } = null!;
    public double InputDuration { get; set; }
    public int InputWidth { get; set; }
    public int InputHeight { get; set; }
    public double InputFps { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> FramePaths { get; set; } = [];
    public string? OcrTitle { get; set; }
    public double OcrConfidence { get; set; }
    public string? OutputVideoPath { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string? AdSetInfo { get; set; }
    public string? DoggyDetail { get; set; }
    public string? PopupDetail { get; set; }
    public string? TvcDetail { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
}
