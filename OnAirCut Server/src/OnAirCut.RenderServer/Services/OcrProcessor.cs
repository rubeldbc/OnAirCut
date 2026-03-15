using System.IO;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tesseract;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class OcrProcessor : IDisposable
{
    private readonly ISettingsService _settingsService;
    private System.Diagnostics.Process? _ocrServer;
    private bool _ocrServerReady;
    private readonly SemaphoreSlim _ocrLock = new(1, 1);

    public OcrProcessor(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<(string Title, double Confidence)> ProcessMultiFrameAsync(
        List<string> framePaths, OcrProfile? profile, string jobId,
        CancellationToken cancellationToken = default)
    {
        if (framePaths.Count == 0 || profile == null)
        {
            return (GenerateFallbackTitle(), 0.0);
        }

        var settings = _settingsService.Settings;
        var ocrCount = Math.Min(settings.OcrMultiFrameCount, framePaths.Count);

        // Pick evenly spaced frames for OCR
        var selectedFrames = new List<string>();
        if (framePaths.Count <= ocrCount)
        {
            selectedFrames.AddRange(framePaths);
        }
        else
        {
            var step = (double)framePaths.Count / ocrCount;
            for (int i = 0; i < ocrCount; i++)
            {
                var idx = (int)(i * step);
                selectedFrames.Add(framePaths[Math.Min(idx, framePaths.Count - 1)]);
            }
        }

        var results = new List<(string Text, double Confidence)>();

        foreach (var framePath in selectedFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var (text, confidence) = await ProcessSingleFrameAsync(framePath, profile, cancellationToken);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add((text, confidence));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "OCR failed for frame: {Path}", framePath);
            }
        }

        if (results.Count == 0)
        {
            return (GenerateFallbackTitle(), 0.0);
        }

        // Consensus: pick the most frequent result
        var grouped = results
            .GroupBy(r => r.Text.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Max(r => r.Confidence))
            .First();

        var bestTitle = grouped.First().Text;
        var bestConfidence = grouped.Max(r => r.Confidence);

        Log.Information("OCR consensus for {JobId}: '{Title}' (confidence: {Conf:F1}%, {Count}/{Total} frames)",
            jobId, bestTitle, bestConfidence, grouped.Count(), results.Count);

        return (bestTitle, bestConfidence);
    }

    private async Task<(string Text, double Confidence)> ProcessSingleFrameAsync(
        string framePath, OcrProfile profile, CancellationToken cancellationToken)
    {
        // Load and preprocess image
        var processedImagePath = framePath + ".ocr.png";

        try
        {
            using (var image = await Image.LoadAsync<Rgba32>(framePath, cancellationToken))
            {
                // Crop region
                if (profile.CropWidth > 0 && profile.CropHeight > 0)
                {
                    var cropRect = new Rectangle(profile.CropX, profile.CropY,
                        Math.Min(profile.CropWidth, image.Width - profile.CropX),
                        Math.Min(profile.CropHeight, image.Height - profile.CropY));
                    image.Mutate(ctx => ctx.Crop(cropRect));
                }

                // Resize
                if (profile.ResizeScale > 1.0)
                {
                    var newWidth = (int)(image.Width * profile.ResizeScale);
                    var newHeight = (int)(image.Height * profile.ResizeScale);
                    image.Mutate(ctx => ctx.Resize(newWidth, newHeight));
                }

                // Grayscale
                image.Mutate(ctx => ctx.Grayscale());

                // Threshold
                if (profile.ThresholdMode == ThresholdMode.Binary)
                {
                    image.Mutate(ctx => ctx.BinaryThreshold(0.5f));
                }
                else if (profile.ThresholdMode == ThresholdMode.Adaptive)
                {
                    image.Mutate(ctx => ctx.AdaptiveThreshold());
                }

                await image.SaveAsPngAsync(processedImagePath, cancellationToken);
            }

            // Try EasyOCR first (best Bengali accuracy)
            var easyOcrResult = await RunEasyOcrAsync(processedImagePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(easyOcrResult))
            {
                Log.Debug("EasyOCR result for frame: '{Text}'", easyOcrResult);
                return (easyOcrResult, 90.0); // EasyOCR doesn't provide confidence, assume high
            }

            // Fallback: Tesseract
            var tessdataPath = _settingsService.Settings.OcrEnginePath;
            var language = _settingsService.Settings.OcrLanguage;

            if (string.IsNullOrEmpty(tessdataPath) || !Directory.Exists(tessdataPath))
            {
                Log.Warning("Tesseract data path not configured or not found: {Path}", tessdataPath);
                return (string.Empty, 0.0);
            }

            using var engine = new TesseractEngine(tessdataPath, language, EngineMode.Default);
            using var pix = Pix.LoadFromFile(processedImagePath);
            using var page = engine.Process(pix);

            var text = page.GetText()?.Trim() ?? string.Empty;
            var confidence = page.GetMeanConfidence() * 100.0;

            return (text, confidence);
        }
        finally
        {
            // Clean up processed image
            try { if (File.Exists(processedImagePath)) File.Delete(processedImagePath); }
            catch { /* ignore cleanup errors */ }
        }
    }

    /// <summary>
    /// Run EasyOCR via a persistent Python server process.
    /// First call starts the server and loads models (~30 sec).
    /// Subsequent calls are instant (~1-2 sec per image).
    /// </summary>
    private async Task<string> RunEasyOcrAsync(string imagePath, CancellationToken ct)
    {
        await _ocrLock.WaitAsync(ct);
        try
        {
            // Start the OCR server if not running
            if (!_ocrServerReady || _ocrServer is null || _ocrServer.HasExited)
            {
                await StartOcrServerAsync(ct);
                if (!_ocrServerReady)
                    return string.Empty;
            }

            // Send image path to the server
            await _ocrServer!.StandardInput.WriteLineAsync(imagePath);
            await _ocrServer.StandardInput.FlushAsync();

            // Read response (with timeout)
            var responseTask = _ocrServer.StandardOutput.ReadLineAsync(ct);
            var completed = await Task.WhenAny(responseTask.AsTask(), Task.Delay(60000, ct));

            if (completed != responseTask.AsTask())
            {
                Log.Warning("EasyOCR server timeout");
                return string.Empty;
            }

            var response = await responseTask;
            if (string.IsNullOrWhiteSpace(response))
                return string.Empty;

            // Parse: "OK|confidence|text" or "EMPTY|0|" or "ERROR|msg"
            var parts = response.Split('|', 3);
            if (parts.Length >= 3 && parts[0] == "OK")
                return parts[2].Trim();

            if (parts[0] == "ERROR")
                Log.Warning("EasyOCR error: {Msg}", parts.Length > 1 ? parts[1] : "unknown");

            return string.Empty;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EasyOCR server call failed");
            return string.Empty;
        }
        finally
        {
            _ocrLock.Release();
        }
    }

    private async Task StartOcrServerAsync(CancellationToken ct)
    {
        _ocrServerReady = false;

        var appBase = AppDomain.CurrentDomain.BaseDirectory;
        var serverScript = Path.Combine(appBase, "lib", "ocr", "ocr_server.py");
        if (!File.Exists(serverScript))
        {
            Log.Debug("EasyOCR server script not found: {Path}", serverScript);
            return;
        }

        var pythonExe = FindPython();
        if (string.IsNullOrEmpty(pythonExe))
        {
            Log.Debug("Python not found for EasyOCR server");
            return;
        }

        Log.Information("Starting EasyOCR server (first load takes ~30 seconds)...");

        _ocrServer?.Kill();
        _ocrServer?.Dispose();

        _ocrServer = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{serverScript}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };
        _ocrServer.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        _ocrServer.Start();

        // Wait for "READY" signal (model loading takes time)
        while (!ct.IsCancellationRequested)
        {
            var readTask = _ocrServer.StandardOutput.ReadLineAsync(ct);
            var timeout = Task.Delay(120000, ct); // 2 min max wait
            var done = await Task.WhenAny(readTask.AsTask(), timeout);

            if (done == timeout)
            {
                Log.Error("EasyOCR server failed to start within 2 minutes");
                return;
            }

            var line = await readTask;
            if (line == "READY")
            {
                _ocrServerReady = true;
                Log.Information("EasyOCR server ready");
                return;
            }
            if (line?.StartsWith("ERROR") == true)
            {
                Log.Error("EasyOCR server error: {Line}", line);
                return;
            }
            // "LOADING" — keep waiting
        }
    }

    private static string? FindPython()
    {
        // 1. First check for bundled portable Python (lib/python/python.exe)
        var appBase = AppDomain.CurrentDomain.BaseDirectory;
        var bundledPython = Path.Combine(appBase, "lib", "python", "python.exe");
        if (File.Exists(bundledPython))
        {
            Log.Debug("Using bundled Python: {Path}", bundledPython);
            return bundledPython;
        }

        // 2. Fallback to system Python
        var candidates = new[] { "python", "python3", "py" };
        foreach (var cmd in candidates)
        {
            try
            {
                var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0) return cmd;
            }
            catch { }
        }
        return null;
    }

    public void Dispose()
    {
        if (_ocrServer is not null && !_ocrServer.HasExited)
        {
            try
            {
                _ocrServer.StandardInput.WriteLine("QUIT");
                _ocrServer.WaitForExit(3000);
                if (!_ocrServer.HasExited) _ocrServer.Kill();
            }
            catch { }
            _ocrServer.Dispose();
        }
        _ocrLock.Dispose();
    }

    private static string GenerateFallbackTitle()
    {
        return $"Story_{DateTime.Now:yyyyMMdd}_{DateTime.Now:HHmmss}";
    }
}
