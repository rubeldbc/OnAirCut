using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using OnAirCut.Core.Constants;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class TextCaptureService : IDisposable
{
    private readonly ISettingsService _settingsService;
    private System.Diagnostics.Process? _ocrServer;
    private bool _ocrServerReady;
    private readonly SemaphoreSlim _ocrLock = new(1, 1);

    public TextCaptureService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Capture a frame from the current MediaPlayer via VLC's TakeSnapshot.
    /// Returns the snapshot as a frozen BitmapSource, or null on failure.
    /// </summary>
    public async Task<BitmapSource?> CaptureFrameAsync(
        LibVLCSharp.Shared.MediaPlayer? mediaPlayer,
        CancellationToken ct = default)
    {
        if (mediaPlayer is null || !mediaPlayer.IsPlaying)
            return null;

        var tempDir = Path.Combine(AppPaths.BaseDirectory, "temp");
        Directory.CreateDirectory(tempDir);

        // Clean up old snapshot files to prevent buildup
        try
        {
            foreach (var old in Directory.GetFiles(tempDir, "snapshot_*.png"))
                File.Delete(old);
            foreach (var old in Directory.GetFiles(tempDir, "ocr_capture_*.png"))
                File.Delete(old);
        }
        catch { }

        var snapshotPath = Path.Combine(tempDir, $"snapshot_{Guid.NewGuid():N}.png");

        try
        {
            mediaPlayer.TakeSnapshot(0, snapshotPath, 0, 0);

            // Wait for the file to be written
            await Task.Delay(500, ct);

            if (File.Exists(snapshotPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(snapshotPath);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Frame capture failed");
        }
        finally
        {
            try
            {
                if (File.Exists(snapshotPath))
                    File.Delete(snapshotPath);
            }
            catch { /* best effort cleanup */ }
        }

        return null;
    }

    /// <summary>
    /// Capture text from a video frame using the configured OCR region.
    /// Uses Tesseract CLI from lib/tesseract or common system paths.
    /// Falls back to the Tesseract NuGet library if CLI is not found.
    /// </summary>
    public async Task<string> CaptureTextFromFrameAsync(
        BitmapSource frame,
        CancellationToken ct = default)
    {
        var settings = _settingsService.Settings;
        var cropX = settings.OcrCropX;
        var cropY = settings.OcrCropY;
        var cropW = settings.OcrCropWidth;
        var cropH = settings.OcrCropHeight;

        if (cropW <= 0 || cropH <= 0)
        {
            Log.Warning("OCR region not configured (OcrCropWidth={W}, OcrCropHeight={H})", cropW, cropH);
            return string.Empty;
        }

        // Ensure minimum height of 100px for reliable OCR detection.
        // Thin strips (< 100px) cause EasyOCR's CRAFT detector to miss text.
        const int minHeight = 100;
        if (cropH < minHeight)
        {
            var extraH = minHeight - cropH;
            cropY = Math.Max(0, cropY - extraH / 2);
            cropH = minHeight;
        }

        // Clamp to image bounds
        cropX = Math.Max(0, Math.Min(cropX, frame.PixelWidth - 1));
        cropY = Math.Max(0, Math.Min(cropY, frame.PixelHeight - 1));
        cropW = Math.Min(cropW, frame.PixelWidth - cropX);
        cropH = Math.Min(cropH, frame.PixelHeight - cropY);

        // Crop the region
        var cropped = new CroppedBitmap(frame, new Int32Rect(cropX, cropY, cropW, cropH));

        // Save cropped image to temp file
        var tempDir = Path.Combine(AppPaths.BaseDirectory, "temp");
        Directory.CreateDirectory(tempDir);
        var tempImage = Path.Combine(tempDir, $"ocr_capture_{Guid.NewGuid():N}.png");
        var tempOutput = Path.Combine(tempDir, $"ocr_output_{Guid.NewGuid():N}");

        try
        {
            // Save cropped bitmap as PNG
            using (var fs = new FileStream(tempImage, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(cropped));
                encoder.Save(fs);
            }

            // Try EasyOCR (Python) first — best Bengali accuracy
            var easyOcrResult = await RunEasyOcrAsync(tempImage, ct);
            if (!string.IsNullOrWhiteSpace(easyOcrResult))
                return easyOcrResult;

            // Fallback: Tesseract NuGet library
            var tessdataPath = AppPaths.TessdataDirectory;
            return await RunTesseractLibraryAsync(tempImage, tessdataPath, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OCR capture failed");
            return string.Empty;
        }
        finally
        {
            try { if (File.Exists(tempImage)) File.Delete(tempImage); } catch { }
        }
    }

    private static async Task<string> RunTesseractCliAsync(
        string tesseractExe,
        string tempImage,
        string tempOutput,
        string tessdataPath,
        CancellationToken ct)
    {
        try
        {
            var args = $"\"{tempImage}\" \"{tempOutput}\" --tessdata-dir \"{tessdataPath}\" --psm 7";

            // Add language if tessdata contains Bengali
            var benTrainedData = Path.Combine(tessdataPath, "ben.traineddata");
            if (File.Exists(benTrainedData))
                args = $"\"{tempImage}\" \"{tempOutput}\" -l ben --tessdata-dir \"{tessdataPath}\" --psm 7";

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tesseractExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            using (ct.Register(() => { try { process.Kill(); } catch { } }))
            {
                await Task.Run(() => process.WaitForExit(15000), ct);
            }

            if (!string.IsNullOrEmpty(stderr) && process.ExitCode != 0)
                Log.Warning("Tesseract CLI stderr: {Stderr}", stderr);

            var outputFile = tempOutput + ".txt";
            if (File.Exists(outputFile))
            {
                var text = (await File.ReadAllTextAsync(outputFile, ct)).Trim();
                try { File.Delete(outputFile); } catch { }
                return text;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tesseract CLI OCR failed");
        }

        return string.Empty;
    }

    private static async Task<string> RunTesseractLibraryAsync(
        string imagePath,
        string tessdataPath,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Determine language: prefer Bengali if trained data exists
                var lang = File.Exists(Path.Combine(tessdataPath, "ben.traineddata")) ? "ben" : "eng";

                using var engine = new Tesseract.TesseractEngine(tessdataPath, lang, Tesseract.EngineMode.LstmOnly);

                // Preprocess: grayscale → scale 3x → binarize
                using var rawImg = Tesseract.Pix.LoadFromFile(imagePath);
                using var grayImg = rawImg.ConvertRGBToGray();
                using var scaledImg = grayImg.Scale(3.0f, 3.0f);
                using var binImg = scaledImg.BinarizeOtsuAdaptiveThreshold(2000, 2000, 0, 0, 0.0f);
                using var finalImg = binImg ?? scaledImg;

                // Try multiple modes, pick best
                var bestText = string.Empty;
                var bestConf = 0f;
                foreach (var psm in new[] { Tesseract.PageSegMode.SingleBlock, Tesseract.PageSegMode.SingleLine })
                {
                    try
                    {
                        using var page = engine.Process(finalImg, psm);
                        var t = page.GetText()?.Trim() ?? string.Empty;
                        var c = page.GetMeanConfidence();
                        if (!string.IsNullOrWhiteSpace(t) && c > bestConf)
                        { bestText = t; bestConf = c; }
                    }
                    catch { }
                }
                // Also try raw image
                try
                {
                    using var page = engine.Process(rawImg, Tesseract.PageSegMode.SingleBlock);
                    var t = page.GetText()?.Trim() ?? string.Empty;
                    var c = page.GetMeanConfidence();
                    if (!string.IsNullOrWhiteSpace(t) && c > bestConf)
                    { bestText = t; bestConf = c; }
                }
                catch { }
                return bestText;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Tesseract library OCR failed");
                return string.Empty;
            }
        }, ct);
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

            // Generate unique request ID to match response
            var reqId = Guid.NewGuid().ToString("N")[..8];

            // Send request with ID: REQ|{id}|{path}
            await _ocrServer!.StandardInput.WriteLineAsync($"REQ|{reqId}|{imagePath}");
            await _ocrServer.StandardInput.FlushAsync();

            // Read lines until we get OUR response (skip any stray library output)
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                var responseTask = _ocrServer.StandardOutput.ReadLineAsync(ct);
                var timeout = Task.Delay(Math.Max(1000, (int)(deadline - DateTime.UtcNow).TotalMilliseconds), ct);
                var completed = await Task.WhenAny(responseTask.AsTask(), timeout);

                if (completed != responseTask.AsTask())
                {
                    Log.Warning("EasyOCR server timeout waiting for response {ReqId}", reqId);
                    return string.Empty;
                }

                var response = await responseTask;
                if (string.IsNullOrWhiteSpace(response))
                    continue;

                // Skip any line that's not our response
                if (!response.StartsWith($"RES|{reqId}|"))
                {
                    Log.Debug("EasyOCR skipping stray output: {Line}", response.Length > 80 ? response[..80] : response);
                    continue;
                }

                // Parse: RES|{id}|OK|{confidence}|{text} or RES|{id}|EMPTY or RES|{id}|ERROR|{msg}
                var parts = response.Split('|', 5);
                // parts[0]=RES, parts[1]=reqId, parts[2]=status
                if (parts.Length >= 5 && parts[2] == "OK")
                    return parts[4].Trim();

                if (parts[2] == "ERROR")
                    Log.Warning("EasyOCR error: {Msg}", parts.Length > 3 ? parts[3] : "unknown");

                return string.Empty; // EMPTY or ERROR
            }

            Log.Warning("EasyOCR server: no matching response for {ReqId}", reqId);

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

        var serverScript = Path.Combine(AppPaths.LibDirectory, "ocr", "ocr_server.py");
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

    private static string? FindPython()
    {
        // 1. First check for bundled portable Python (lib/python/python.exe)
        var bundledPython = Path.Combine(AppPaths.LibDirectory, "python", "python.exe");
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

    private static string? FindTesseract()
    {
        var paths = new[]
        {
            Path.Combine(AppPaths.LibDirectory, "tesseract", "tesseract.exe"),
            @"C:\Program Files\Tesseract-OCR\tesseract.exe",
            @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
        };
        return paths.FirstOrDefault(File.Exists);
    }
}
