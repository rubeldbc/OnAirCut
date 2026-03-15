using System.IO;
using System.Text.Json;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Utilities;
using OnAirCut.RenderServer.Models;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class OutputOrganizer
{
    private readonly ISharedFolderService _sharedFolderService;

    public OutputOrganizer(ISharedFolderService sharedFolderService)
    {
        _sharedFolderService = sharedFolderService;
    }

    public async Task<(string OutputFolderPath, string OutputVideoPath, string FramesOutputPath)> OrganizeOutputAsync(
        JobContext context, string renderedVideoPath, string safeFolderName,
        CancellationToken cancellationToken = default)
    {
        // Create output folder: {SharedFolder}/Output/{date}/{SafeFolderName}/
        var dateFolder = _sharedFolderService.GetDateSubfolder(FolderNames.Output, DateTime.Now);

        // Ensure unique folder name
        var uniqueName = TitleSanitizer.EnsureUniqueName(safeFolderName,
            name => Directory.Exists(Path.Combine(dateFolder, name)));

        var outputFolder = Path.Combine(dateFolder, uniqueName);
        Directory.CreateDirectory(outputFolder);

        // Move rendered video
        var outputVideoPath = Path.Combine(outputFolder, $"{uniqueName}.mp4");
        if (File.Exists(renderedVideoPath))
        {
            File.Move(renderedVideoPath, outputVideoPath, overwrite: true);
            Log.Information("Moved rendered video to {Path}", outputVideoPath);
        }

        // Move frames to frames/ subfolder
        var framesOutputPath = Path.Combine(outputFolder, FolderNames.Frames);
        Directory.CreateDirectory(framesOutputPath);

        foreach (var framePath in context.FramePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(framePath))
            {
                var destFrame = Path.Combine(framesOutputPath, Path.GetFileName(framePath));
                try
                {
                    File.Copy(framePath, destFrame, overwrite: true);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to copy frame {Path}", framePath);
                }
            }
        }

        // Generate metadata.json
        var metadata = new
        {
            jobId = context.JobFile.JobId,
            sourceName = context.JobFile.SourceName,
            sourceType = context.JobFile.SourceType.ToString(),
            clipStartTime = context.JobFile.ClipStartTime,
            clipEndTime = context.JobFile.ClipEndTime,
            durationSeconds = context.InputDuration,
            resolution = $"{context.InputWidth}x{context.InputHeight}",
            fps = context.InputFps,
            ocrTitle = context.OcrTitle,
            ocrConfidence = context.OcrConfidence,
            adSetName = context.JobFile.AdSetName,
            overlaySetName = context.JobFile.OverlaySetName,
            submittedBy = context.JobFile.SubmittedBy,
            submittedAt = context.JobFile.SubmittedAt,
            processedAt = DateTime.UtcNow,
            outputVideoPath,
            framesCount = context.FramePaths.Count
        };

        var metadataPath = Path.Combine(outputFolder, "metadata.json");
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

        Log.Information("Output organized at {Path}", outputFolder);
        return (outputFolder, outputVideoPath, framesOutputPath);
    }
}
