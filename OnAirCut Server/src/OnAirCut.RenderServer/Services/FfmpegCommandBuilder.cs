using System.Globalization;
using System.IO;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using OnAirCut.RenderServer.Models;

namespace OnAirCut.RenderServer.Services;

public class FfmpegCommandBuilder
{
    private readonly ISettingsService _settingsService;

    public FfmpegCommandBuilder(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string BuildArguments(string inputPath, AdSetConfig? adSet, string outputPath, double inputDuration)
    {
        var settings = _settingsService.Settings;
        var codec = settings.OutputVideoCodec;
        var preset = settings.OutputVideoPreset;
        var crf = settings.OutputVideoCRF;
        var audioCodec = settings.OutputAudioCodec;
        var audioBitrate = settings.OutputAudioBitrate;

        if (adSet == null || adSet.InsertMode == InsertMode.None)
        {
            // No TVC insertion; just re-encode with overlay if present
            if (!string.IsNullOrEmpty(adSet?.OverlayFile) && File.Exists(adSet.OverlayFile))
            {
                return BuildOverlayOnlyArgs(inputPath, adSet.OverlayFile, adSet.OverlayStartSec,
                    Math.Min(adSet.OverlayEndSec, inputDuration), outputPath, codec, preset, crf, audioCodec, audioBitrate);
            }

            // Simple re-encode
            return $"-y -i \"{inputPath}\" -c:v {codec} -preset {preset} -crf {crf} -c:a {audioCodec} -b:a {audioBitrate} \"{outputPath}\"";
        }

        double insertAt = adSet.InsertMode == InsertMode.Midpoint
            ? inputDuration / 2.0
            : adSet.InsertAtSec;

        var tvcFile = adSet.TvcFile ?? string.Empty;
        var overlayFile = adSet.OverlayFile;

        bool hasTvc = !string.IsNullOrEmpty(tvcFile) && File.Exists(tvcFile);
        bool hasOverlay = !string.IsNullOrEmpty(overlayFile) && File.Exists(overlayFile);

        if (hasTvc && hasOverlay)
        {
            return BuildTvcAndOverlayArgs(inputPath, tvcFile, insertAt, overlayFile!,
                adSet.OverlayStartSec, Math.Min(adSet.OverlayEndSec, inputDuration),
                outputPath, codec, preset, crf, audioCodec, audioBitrate);
        }

        if (hasTvc)
        {
            return BuildTvcOnlyArgs(inputPath, tvcFile, insertAt, outputPath,
                codec, preset, crf, audioCodec, audioBitrate);
        }

        if (hasOverlay)
        {
            return BuildOverlayOnlyArgs(inputPath, overlayFile!, adSet.OverlayStartSec,
                Math.Min(adSet.OverlayEndSec, inputDuration), outputPath, codec, preset, crf, audioCodec, audioBitrate);
        }

        // Fallback: simple re-encode
        return $"-y -i \"{inputPath}\" -c:v {codec} -preset {preset} -crf {crf} -c:a {audioCodec} -b:a {audioBitrate} \"{outputPath}\"";
    }

    private static string BuildOverlayOnlyArgs(string input, string overlay, double overlayStart, double overlayEnd,
        string output, string codec, string preset, int crf, string audioCodec, string audioBitrate)
    {
        var start = overlayStart.ToString("F3", CultureInfo.InvariantCulture);
        var end = overlayEnd.ToString("F3", CultureInfo.InvariantCulture);

        return $"-y -i \"{input}\" -i \"{overlay}\" " +
               $"-filter_complex \"[1:v]format=rgba[ovr];[0:v][ovr]overlay=0:0:enable='between(t,{start},{end})'[outv]\" " +
               $"-map \"[outv]\" -map 0:a? -c:v {codec} -preset {preset} -crf {crf} -c:a {audioCodec} -b:a {audioBitrate} \"{output}\"";
    }

    private static string BuildTvcOnlyArgs(string input, string tvc, double insertAt, string output,
        string codec, string preset, int crf, string audioCodec, string audioBitrate)
    {
        var splitTime = insertAt.ToString("F3", CultureInfo.InvariantCulture);

        return $"-y -i \"{input}\" -i \"{tvc}\" " +
               $"-filter_complex \"" +
               $"[0:v]split=2[v1][v2];" +
               $"[0:a]asplit=2[a1][a2];" +
               $"[v1]trim=0:{splitTime},setpts=PTS-STARTPTS[pv1];" +
               $"[a1]atrim=0:{splitTime},asetpts=PTS-STARTPTS[pa1];" +
               $"[v2]trim={splitTime},setpts=PTS-STARTPTS[pv2];" +
               $"[a2]atrim={splitTime},asetpts=PTS-STARTPTS[pa2];" +
               $"[1:v]setpts=PTS-STARTPTS[tvcv];" +
               $"[1:a]asetpts=PTS-STARTPTS[tvca];" +
               $"[pv1][pa1][tvcv][tvca][pv2][pa2]concat=n=3:v=1:a=1[outv][outa]\" " +
               $"-map \"[outv]\" -map \"[outa]\" -c:v {codec} -preset {preset} -crf {crf} -c:a {audioCodec} -b:a {audioBitrate} \"{output}\"";
    }

    private static string BuildTvcAndOverlayArgs(string input, string tvc, double insertAt,
        string overlay, double overlayStart, double overlayEnd, string output,
        string codec, string preset, int crf, string audioCodec, string audioBitrate)
    {
        var splitTime = insertAt.ToString("F3", CultureInfo.InvariantCulture);
        var ovrStart = overlayStart.ToString("F3", CultureInfo.InvariantCulture);
        var ovrEnd = overlayEnd.ToString("F3", CultureInfo.InvariantCulture);

        return $"-y -i \"{input}\" -i \"{tvc}\" -i \"{overlay}\" " +
               $"-filter_complex \"" +
               $"[0:v]split=2[v1][v2];" +
               $"[0:a]asplit=2[a1][a2];" +
               $"[v1]trim=0:{splitTime},setpts=PTS-STARTPTS[pv1];" +
               $"[a1]atrim=0:{splitTime},asetpts=PTS-STARTPTS[pa1];" +
               $"[v2]trim={splitTime},setpts=PTS-STARTPTS[pv2];" +
               $"[a2]atrim={splitTime},asetpts=PTS-STARTPTS[pa2];" +
               $"[1:v]setpts=PTS-STARTPTS[tvcv];" +
               $"[1:a]asetpts=PTS-STARTPTS[tvca];" +
               $"[pv1][pa1][tvcv][tvca][pv2][pa2]concat=n=3:v=1:a=1[concv][conca];" +
               $"[2:v]format=rgba[ovrv];" +
               $"[concv][ovrv]overlay=0:0:enable='between(t,{ovrStart},{ovrEnd})'[outv]\" " +
               $"-map \"[outv]\" -map \"[conca]\" -c:v {codec} -preset {preset} -crf {crf} -c:a {audioCodec} -b:a {audioBitrate} \"{output}\"";
    }
}
