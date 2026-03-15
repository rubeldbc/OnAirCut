using System.Globalization;
using System.IO;
using System.Text;
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
        var s = _settingsService.Settings;
        var codec = s.OutputVideoCodec;
        var preset = s.OutputVideoPreset;
        var crf = s.OutputVideoCRF;
        var aCodec = s.OutputAudioCodec;
        var aBitrate = s.OutputAudioBitrate;
        var encArgs = $"-c:v {codec} -preset {preset} -crf {crf} -c:a {aCodec} -b:a {aBitrate}";

        if (adSet is null || !adSet.HasAnyEnabled)
            return $"-y -i \"{inputPath}\" {encArgs} \"{outputPath}\"";

        var hasDoggy = adSet.Doggy is { Enabled: true, File: not null } && File.Exists(adSet.Doggy.File);
        var hasPopup = adSet.Popup is { Enabled: true, File: not null } && File.Exists(adSet.Popup.File);
        var hasTvc = adSet.Tvc is { Enabled: true, File: not null } && File.Exists(adSet.Tvc.File);

        // Simple re-encode if nothing valid
        if (!hasDoggy && !hasPopup && !hasTvc)
            return $"-y -i \"{inputPath}\" {encArgs} \"{outputPath}\"";

        // Build inputs and filter graph
        var inputs = new StringBuilder();
        var filters = new StringBuilder();
        inputs.Append($"-i \"{inputPath}\"");
        int inputIdx = 1;
        int doggyIdx = -1, popupIdx = -1, tvcIdx = -1;

        if (hasTvc) { inputs.Append($" -i \"{adSet.Tvc!.File}\""); tvcIdx = inputIdx++; }
        if (hasDoggy) { inputs.Append($" -i \"{adSet.Doggy!.File}\""); doggyIdx = inputIdx++; }
        if (hasPopup) { inputs.Append($" -i \"{adSet.Popup!.File}\""); popupIdx = inputIdx++; }

        var lastVideoLabel = "0:v";
        var lastAudioLabel = "0:a";

        // ---- TVC: split main video and concat with TVC insertions ----
        if (hasTvc && adSet.Tvc!.Count > 0)
        {
            var n = adSet.Tvc.Count;
            // Calculate insertion points evenly distributed
            var splits = new double[n];
            for (int i = 0; i < n; i++)
                splits[i] = inputDuration * (i + 1) / (n + 1);

            // Split main video into n+1 segments
            var segCount = n + 1;
            filters.Append($"[0:v]split={segCount}");
            for (int i = 0; i < segCount; i++) filters.Append($"[sv{i}]");
            filters.Append(';');
            filters.Append($"[0:a]asplit={segCount}");
            for (int i = 0; i < segCount; i++) filters.Append($"[sa{i}]");
            filters.Append(';');

            // Trim each segment
            double prev = 0;
            for (int i = 0; i < segCount; i++)
            {
                double start = prev;
                double end = i < n ? splits[i] : inputDuration;
                filters.Append($"[sv{i}]trim={F(start)}:{F(end)},setpts=PTS-STARTPTS[tv{i}];");
                filters.Append($"[sa{i}]atrim={F(start)}:{F(end)},asetpts=PTS-STARTPTS[ta{i}];");
                prev = end;
            }

            // Prepare TVC segments
            filters.Append($"[{tvcIdx}:v]setpts=PTS-STARTPTS[tvcv];");
            filters.Append($"[{tvcIdx}:a]asetpts=PTS-STARTPTS[tvca];");

            // Build concat: seg0 + tvc + seg1 + tvc + ... + segN
            var concatInputs = new StringBuilder();
            var totalSegments = 0;
            for (int i = 0; i < segCount; i++)
            {
                concatInputs.Append($"[tv{i}][ta{i}]");
                totalSegments++;
                if (i < n) // insert TVC after each segment except last
                {
                    concatInputs.Append("[tvcv][tvca]");
                    totalSegments++;
                }
            }
            filters.Append($"{concatInputs}concat=n={totalSegments}:v=1:a=1[concv][conca];");
            lastVideoLabel = "concv";
            lastAudioLabel = "conca";
        }

        // ---- Doggy: overlay with crop, position, opacity ----
        if (hasDoggy)
        {
            var d = adSet.Doggy!;
            var cropW = Math.Max(1, d.Width - d.CropLeft - d.CropRight);
            var cropH = Math.Max(1, d.Height - d.CropTop - d.CropBottom);
            var startEnable = d.StartFrom > 0 ? $":enable='gte(t,{F(d.StartFrom)})'" : "";
            var alpha = d.Opacity < 1.0 ? $",colorchannelmixer=aa={F(d.Opacity)}" : "";

            filters.Append($"[{doggyIdx}:v]format=rgba,scale={F(d.Width)}:{F(d.Height)}");
            if (d.CropTop > 0 || d.CropRight > 0 || d.CropBottom > 0 || d.CropLeft > 0)
                filters.Append($",crop={F(cropW)}:{F(cropH)}:{F(d.CropLeft)}:{F(d.CropTop)}");
            filters.Append($"{alpha}[doggy];");
            filters.Append($"[{lastVideoLabel}][doggy]overlay={F(d.PositionX + d.CropLeft)}:{F(d.PositionY + d.CropTop)}{startEnable}[dout];");
            lastVideoLabel = "dout";
        }

        // ---- Popup: overlay with crop, position, opacity, timed appearances ----
        if (hasPopup)
        {
            var p = adSet.Popup!;
            var cropW = Math.Max(1, p.Width - p.CropLeft - p.CropRight);
            var cropH = Math.Max(1, p.Height - p.CropTop - p.CropBottom);
            var alpha = p.Opacity < 1.0 ? $",colorchannelmixer=aa={F(p.Opacity)}" : "";

            filters.Append($"[{popupIdx}:v]format=rgba,scale={F(p.Width)}:{F(p.Height)}");
            if (p.CropTop > 0 || p.CropRight > 0 || p.CropBottom > 0 || p.CropLeft > 0)
                filters.Append($",crop={F(cropW)}:{F(cropH)}:{F(p.CropLeft)}:{F(p.CropTop)}");
            filters.Append($"{alpha}[popup];");

            // Calculate timed enable expression
            var totalDur = hasTvc ? inputDuration + adSet.Tvc!.Count * 30 : inputDuration; // approx
            var enableExpr = BuildPopupEnableExpr(p, totalDur);
            filters.Append($"[{lastVideoLabel}][popup]overlay={F(p.PositionX + p.CropLeft)}:{F(p.PositionY + p.CropTop)}:enable='{enableExpr}'[pout];");
            lastVideoLabel = "pout";
        }

        // Remove trailing semicolon from filters
        var filterStr = filters.ToString().TrimEnd(';');

        return $"-y {inputs} -filter_complex \"{filterStr}\" -map \"[{lastVideoLabel}]\" -map \"[{lastAudioLabel}]\" {encArgs} \"{outputPath}\"";
    }

    private static string BuildPopupEnableExpr(PopupAdConfig p, double totalDuration)
    {
        if (p.TotalPlay <= 0) return "0";
        var interval = (totalDuration - p.StartFrom) / p.TotalPlay;
        var parts = new List<string>();
        for (int i = 0; i < p.TotalPlay; i++)
        {
            var start = p.StartFrom + i * interval;
            var end = start + p.DurationPerTime;
            parts.Add($"between(t,{F(start)},{F(end)})");
        }
        return string.Join("+", parts);
    }

    private static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
