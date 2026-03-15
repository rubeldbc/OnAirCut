using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using OnAirCut.RenderServer.Models;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class FrameExtractionService
{
    private readonly ISettingsService _settingsService;

    public FrameExtractionService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<(double Duration, int Width, int Height, double Fps, string VideoCodec)> ProbeInputAsync(
        string inputPath, CancellationToken cancellationToken = default)
    {
        var ffprobe = _settingsService.Settings.FFprobePath;

        var result = await Cli.Wrap(ffprobe)
            .WithArguments($"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"")
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cancellationToken);

        double duration = 0;
        int width = 0, height = 0;
        double fps = 0;
        string videoCodec = "unknown";

        var output = result.StandardOutput;

        // Parse duration
        var durationMatch = Regex.Match(output, "\"duration\"\\s*:\\s*\"([\\d.]+)\"");
        if (durationMatch.Success)
            double.TryParse(durationMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out duration);

        // Parse video stream
        var widthMatch = Regex.Match(output, "\"width\"\\s*:\\s*(\\d+)");
        if (widthMatch.Success)
            int.TryParse(widthMatch.Groups[1].Value, out width);

        var heightMatch = Regex.Match(output, "\"height\"\\s*:\\s*(\\d+)");
        if (heightMatch.Success)
            int.TryParse(heightMatch.Groups[1].Value, out height);

        var fpsMatch = Regex.Match(output, "\"r_frame_rate\"\\s*:\\s*\"(\\d+)/(\\d+)\"");
        if (fpsMatch.Success)
        {
            if (double.TryParse(fpsMatch.Groups[1].Value, out var num) &&
                double.TryParse(fpsMatch.Groups[2].Value, out var den) && den > 0)
            {
                fps = num / den;
            }
        }

        var codecMatch = Regex.Match(output, "\"codec_name\"\\s*:\\s*\"([^\"]+)\"");
        if (codecMatch.Success)
            videoCodec = codecMatch.Groups[1].Value;

        // Fallback for duration from format
        if (duration <= 0)
        {
            var formatDur = Regex.Match(output, "\"duration\"\\s*:\\s*\"?([\\d.]+)\"?");
            if (formatDur.Success)
                double.TryParse(formatDur.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out duration);
        }

        Log.Information("Probed {Path}: {Duration}s, {Width}x{Height}, {Fps}fps, {Codec}",
            inputPath, duration, width, height, fps, videoCodec);

        return (duration, width, height, fps, videoCodec);
    }

    public async Task<List<string>> ExtractFramesAsync(string inputPath, string outputDir, int frameCount,
        double duration, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);

        var ffmpeg = _settingsService.Settings.FFmpegPath;
        var outputPattern = Path.Combine(outputDir, "frame_%04d.jpg");

        // Calculate fps filter value for even distribution
        double fpsValue = frameCount / Math.Max(duration, 1.0);
        var fpsFilter = fpsValue.ToString("F6", CultureInfo.InvariantCulture);

        var args = $"-i \"{inputPath}\" -vf \"fps={fpsFilter}\" -frames:v {frameCount} -q:v 2 \"{outputPattern}\"";

        Log.Information("Extracting {Count} frames from {Path}", frameCount, inputPath);

        var stderr = new StringBuilder();
        var result = await Cli.Wrap(ffmpeg)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .ExecuteAsync(cancellationToken);

        if (result.ExitCode != 0)
        {
            Log.Warning("FFmpeg frame extraction exited with code {Code}: {Error}", result.ExitCode, stderr);
        }

        // Collect extracted frames
        var frames = new List<string>();
        if (Directory.Exists(outputDir))
        {
            frames = Directory.GetFiles(outputDir, "frame_*.jpg")
                .OrderBy(f => f)
                .ToList();
        }

        Log.Information("Extracted {Count} frames to {Dir}", frames.Count, outputDir);
        return frames;
    }
}
