using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class FfmpegRenderService
{
    private readonly ISettingsService _settingsService;

    public FfmpegRenderService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public event EventHandler<RenderProgressEventArgs>? ProgressChanged;

    public async Task<(bool Success, string? ErrorMessage)> RenderAsync(string arguments, double totalDuration,
        CancellationToken cancellationToken = default)
    {
        var ffmpeg = _settingsService.Settings.FFmpegPath;
        var errorOutput = new StringBuilder();

        Log.Information("Starting FFmpeg render: {Ffmpeg} {Args}", ffmpeg, arguments);

        try
        {
            var result = await Cli.Wrap(ffmpeg)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    ParseProgressLine(line, totalDuration);
                    errorOutput.AppendLine(line);
                }))
                .ExecuteAsync(cancellationToken);

            if (result.ExitCode != 0)
            {
                var error = errorOutput.ToString();
                Log.Error("FFmpeg exited with code {Code}: {Error}", result.ExitCode, error);
                return (false, $"FFmpeg exit code {result.ExitCode}: {GetLastError(error)}");
            }

            Log.Information("FFmpeg render completed successfully");
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("FFmpeg render was cancelled");
            return (false, "Render cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FFmpeg render failed");
            return (false, ex.Message);
        }
    }

    private void ParseProgressLine(string line, double totalDuration)
    {
        if (string.IsNullOrEmpty(line) || totalDuration <= 0) return;

        // Parse time=HH:MM:SS.ms
        var timeMatch = Regex.Match(line, @"time=(\d+):(\d+):(\d+)\.(\d+)");
        if (!timeMatch.Success) return;

        if (int.TryParse(timeMatch.Groups[1].Value, out var hours) &&
            int.TryParse(timeMatch.Groups[2].Value, out var minutes) &&
            int.TryParse(timeMatch.Groups[3].Value, out var seconds) &&
            int.TryParse(timeMatch.Groups[4].Value, out var centiseconds))
        {
            var currentTime = hours * 3600.0 + minutes * 60.0 + seconds + centiseconds / 100.0;
            var progress = Math.Min(100.0, currentTime / totalDuration * 100.0);

            // Parse speed
            string speed = "N/A";
            var speedMatch = Regex.Match(line, @"speed=\s*([\d.]+)x");
            if (speedMatch.Success)
                speed = speedMatch.Groups[1].Value + "x";

            // Parse frame
            int frame = 0;
            var frameMatch = Regex.Match(line, @"frame=\s*(\d+)");
            if (frameMatch.Success)
                int.TryParse(frameMatch.Groups[1].Value, out frame);

            ProgressChanged?.Invoke(this, new RenderProgressEventArgs
            {
                Progress = progress,
                CurrentTime = currentTime,
                TotalDuration = totalDuration,
                Speed = speed,
                Frame = frame
            });
        }
    }

    private static string GetLastError(string stderr)
    {
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith("frame=") && !line.StartsWith("size="))
                return line;
        }
        return stderr.Length > 500 ? stderr[^500..] : stderr;
    }
}

public class RenderProgressEventArgs : EventArgs
{
    public double Progress { get; init; }
    public double CurrentTime { get; init; }
    public double TotalDuration { get; init; }
    public string Speed { get; init; } = "N/A";
    public int Frame { get; init; }
}
