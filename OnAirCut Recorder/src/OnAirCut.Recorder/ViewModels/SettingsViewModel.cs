using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using OnAirCut.Core.Utilities;
using OnAirCut.Recorder.Helpers;
using OnAirCut.Recorder.Models;
using OnAirCut.Recorder.Services;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IOcrProfileProvider? _ocrProfileProvider;
    private readonly SourcePanelViewModel? _sourcePanelViewModel;

    public SettingsViewModel(
        ISettingsService settingsService,
        IOcrProfileProvider? ocrProfileProvider = null,
        SourcePanelViewModel? sourcePanelViewModel = null)
    {
        _settingsService = settingsService;
        _ocrProfileProvider = ocrProfileProvider;
        _sourcePanelViewModel = sourcePanelViewModel;
        LoadFromSettings();
    }

    // General
    [ObservableProperty]
    private string _operatorName = string.Empty;

    [ObservableProperty]
    private string _renderServerApiUrl = "http://localhost:5123";

    [ObservableProperty]
    private bool _autoSubmitOnRecordStop;

    // Shared Folder
    [ObservableProperty]
    private string _sharedFolderPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FolderValidationItem> _folderValidationItems = [];

    // Recording
    [ObservableProperty]
    private string _recordingFormat = "mp4";

    [ObservableProperty]
    private string _recordingCodec = "libx264";

    [ObservableProperty]
    private string _ffmpegPath = "ffmpeg";

    [ObservableProperty]
    private string _ytDlpPath = "yt-dlp";

    // Audio
    [ObservableProperty]
    private string _audioDevice = string.Empty;

    [ObservableProperty]
    private int _monitorVolume = 70;

    // OCR Region
    [ObservableProperty]
    private BitmapImage? _frameImage;

    [ObservableProperty]
    private int _ocrCropX;

    [ObservableProperty]
    private int _ocrCropY;

    [ObservableProperty]
    private int _ocrCropWidth;

    [ObservableProperty]
    private int _ocrCropHeight;

    [ObservableProperty]
    private string _ocrProfileName = string.Empty;

    [ObservableProperty]
    private string _ocrSourceName = string.Empty;

    [ObservableProperty]
    private double _ocrResizeScale = 2.0;

    [ObservableProperty]
    private ThresholdMode _ocrThresholdMode = ThresholdMode.None;

    [ObservableProperty]
    private ObservableCollection<OcrProfile> _availableProfiles = [];

    [ObservableProperty]
    private OcrProfile? _selectedOcrProfile;

    [ObservableProperty]
    private ObservableCollection<string> _ocrProfileNames = new();

    [ObservableProperty]
    private string? _selectedOcrProfileName;

    [ObservableProperty]
    private string _newProfileName = "";

    [ObservableProperty]
    private string _ocrTestResult = string.Empty;

    // Legacy fields kept for save compatibility
    [ObservableProperty]
    private SourceType _defaultSourceType;

    [ObservableProperty]
    private string _defaultAdSet = string.Empty;

    // UI state
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _connectionTestResult = string.Empty;

    public Array SourceTypes => Enum.GetValues<SourceType>();
    public Array ThresholdModes => Enum.GetValues<ThresholdMode>();

    partial void OnSelectedOcrProfileChanged(OcrProfile? value)
    {
        if (value is null) return;
        OcrProfileName = value.ProfileName;
        OcrSourceName = value.SourceName;
        OcrCropX = value.CropX;
        OcrCropY = value.CropY;
        OcrCropWidth = value.CropWidth;
        OcrCropHeight = value.CropHeight;
        OcrResizeScale = value.ResizeScale;
        OcrThresholdMode = value.ThresholdMode;
    }

    partial void OnSelectedOcrProfileNameChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        _ = LoadOcrProfileFromSharedAsync(value);
    }

    partial void OnSharedFolderPathChanged(string value)
    {
        _ = LoadOcrProfileNamesAsync();
    }

    public async Task LoadOcrProfileNamesAsync()
    {
        try
        {
            var sharedFolder = _settingsService.Settings.SharedFolderPath;
            if (string.IsNullOrWhiteSpace(sharedFolder)) sharedFolder = SharedFolderPath;
            if (string.IsNullOrWhiteSpace(sharedFolder)) return;

            var profileDir = Path.Combine(sharedFolder, "Assets", "OcrProfiles");
            if (!Directory.Exists(profileDir))
            {
                OcrProfileNames.Clear();
                return;
            }

            var names = await Task.Run(() =>
                Directory.GetFiles(profileDir, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .OrderBy(n => n)
                    .ToList());

            OcrProfileNames = new ObservableCollection<string>(names);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load OCR profile names from shared folder");
        }
    }

    [RelayCommand]
    private async Task SaveOcrProfileToSharedAsync()
    {
        var profileName = NewProfileName?.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            OcrTestResult = "Enter a profile name first.";
            return;
        }

        if (OcrCropWidth <= 0 || OcrCropHeight <= 0)
        {
            OcrTestResult = "Define a crop region first.";
            return;
        }

        var sharedFolder = SharedFolderPath;
        if (string.IsNullOrWhiteSpace(sharedFolder))
        {
            OcrTestResult = "Set a shared folder path in Settings first.";
            return;
        }

        try
        {
            var profileDir = Path.Combine(sharedFolder, "Assets", "OcrProfiles");
            Directory.CreateDirectory(profileDir);

            var profileData = new
            {
                profileName,
                cropX = OcrCropX,
                cropY = OcrCropY,
                cropWidth = OcrCropWidth,
                cropHeight = OcrCropHeight
            };

            var json = JsonSerializer.Serialize(profileData, new JsonSerializerOptions { WriteIndented = true });
            var filePath = Path.Combine(profileDir, $"{profileName}.json");
            await File.WriteAllTextAsync(filePath, json);

            OcrTestResult = $"Profile '{profileName}' saved to shared folder.";
            Log.Information("OCR profile saved to shared folder: {Path}", filePath);

            await LoadOcrProfileNamesAsync();
            SelectedOcrProfileName = profileName;

            // Also notify SourcePanelViewModel to refresh its list
            if (_sourcePanelViewModel is not null)
            {
                await _sourcePanelViewModel.LoadOcrProfilesAsync();
            }
        }
        catch (Exception ex)
        {
            OcrTestResult = $"Save failed: {ex.Message}";
            Log.Error(ex, "Failed to save OCR profile to shared folder");
        }
    }

    public async Task LoadOcrProfileFromSharedAsync(string name)
    {
        try
        {
            var sharedFolder = SharedFolderPath;
            if (string.IsNullOrWhiteSpace(sharedFolder)) sharedFolder = _settingsService.Settings.SharedFolderPath;
            if (string.IsNullOrWhiteSpace(sharedFolder)) return;

            var filePath = Path.Combine(sharedFolder, "Assets", "OcrProfiles", $"{name}.json");
            if (!File.Exists(filePath)) return;

            var json = await File.ReadAllTextAsync(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("cropX", out var cropX)) OcrCropX = cropX.GetInt32();
            if (root.TryGetProperty("cropY", out var cropY)) OcrCropY = cropY.GetInt32();
            if (root.TryGetProperty("cropWidth", out var cropW)) OcrCropWidth = cropW.GetInt32();
            if (root.TryGetProperty("cropHeight", out var cropH)) OcrCropHeight = cropH.GetInt32();

            OcrTestResult = $"Profile '{name}' loaded (X:{OcrCropX}, Y:{OcrCropY}, W:{OcrCropWidth}, H:{OcrCropHeight}).";
            Log.Information("OCR profile loaded from shared: {Name}", name);
        }
        catch (Exception ex)
        {
            OcrTestResult = $"Load failed: {ex.Message}";
            Log.Error(ex, "Failed to load OCR profile from shared folder");
        }
    }

    [RelayCommand]
    private async Task DeleteOcrProfileFromSharedAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedOcrProfileName))
        {
            OcrTestResult = "Select a profile to delete first.";
            return;
        }

        var sharedFolder = SharedFolderPath;
        if (string.IsNullOrWhiteSpace(sharedFolder)) return;

        try
        {
            var filePath = Path.Combine(sharedFolder, "Assets", "OcrProfiles", $"{SelectedOcrProfileName}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                OcrTestResult = $"Profile '{SelectedOcrProfileName}' deleted.";
                Log.Information("OCR profile deleted: {Path}", filePath);
            }

            SelectedOcrProfileName = null;
            await LoadOcrProfileNamesAsync();

            if (_sourcePanelViewModel is not null)
            {
                await _sourcePanelViewModel.LoadOcrProfilesAsync();
            }
        }
        catch (Exception ex)
        {
            OcrTestResult = $"Delete failed: {ex.Message}";
            Log.Error(ex, "Failed to delete OCR profile from shared folder");
        }
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;
        SharedFolderPath = s.SharedFolderPath;
        DefaultSourceType = s.DefaultSourceType;
        DefaultAdSet = s.DefaultAdSet;
        RecordingFormat = s.RecordingFormat;
        RecordingCodec = s.RecordingCodec;
        AudioDevice = s.AudioDevice;
        MonitorVolume = s.MonitorVolume;
        OperatorName = s.OperatorName;
        FfmpegPath = s.FFmpegPath;
        YtDlpPath = s.YtDlpPath;
        AutoSubmitOnRecordStop = s.AutoSubmitOnRecordStop;
        RenderServerApiUrl = s.RenderServerApiUrl;
        OcrProfileName = s.OcrProfileName;

        // Load OCR profile names from shared folder (fire-and-forget)
        _ = LoadOcrProfileNamesAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        try
        {
            _settingsService.UpdateSettings(s =>
            {
                s.SharedFolderPath = SharedFolderPath;
                s.DefaultSourceType = DefaultSourceType;
                s.DefaultAdSet = DefaultAdSet;
                s.RecordingFormat = RecordingFormat;
                s.RecordingCodec = RecordingCodec;
                s.AudioDevice = AudioDevice;
                s.MonitorVolume = MonitorVolume;
                s.OperatorName = OperatorName;
                s.FFmpegPath = FfmpegPath;
                s.YtDlpPath = YtDlpPath;
                s.AutoSubmitOnRecordStop = AutoSubmitOnRecordStop;
                s.RenderServerApiUrl = RenderServerApiUrl;
                s.OcrProfileName = OcrProfileName;
            });

            await _settingsService.SaveAsync();
            StatusMessage = "Settings saved successfully";
            Log.Information("Settings saved");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            Log.Error(ex, "Failed to save settings");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        LoadFromSettings();
        StatusMessage = "Settings reset to last saved values";
    }

    [RelayCommand]
    private void BrowseSharedFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Shared Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            SharedFolderPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseFfmpeg()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select FFmpeg Executable",
            Filter = "Executable Files|*.exe|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            FfmpegPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseYtDlp()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select yt-dlp Executable",
            Filter = "Executable Files|*.exe|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            YtDlpPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        ConnectionTestResult = "Testing...";
        try
        {
            var ffmpegOk = await ProcessHelper.IsFFmpegAvailableAsync(FfmpegPath);
            var ytDlpOk = await ProcessHelper.IsYtDlpAvailableAsync(YtDlpPath);

            var results = new List<string>();
            results.Add(ffmpegOk ? "FFmpeg: OK" : "FFmpeg: Not found");
            results.Add(ytDlpOk ? "yt-dlp: OK" : "yt-dlp: Not found");

            if (!string.IsNullOrWhiteSpace(SharedFolderPath))
            {
                var folderOk = Directory.Exists(SharedFolderPath);
                results.Add(folderOk ? "Shared Folder: Accessible" : "Shared Folder: Not accessible");
            }

            ConnectionTestResult = string.Join(" | ", results);
        }
        catch (Exception ex)
        {
            ConnectionTestResult = $"Test failed: {ex.Message}";
        }
    }

    // Shared Folder Validation (Issue 4)
    [RelayCommand]
    private void CreateFolderStructure()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SharedFolderPath))
            {
                StatusMessage = "Please set a shared folder path first.";
                return;
            }

            SharedFolderInitializer.Initialize(SharedFolderPath);
            StatusMessage = "Folder structure created successfully!";
            ValidateSharedFolder();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to create folder structure: {ex.Message}";
            Log.Error(ex, "Failed to create folder structure");
        }
    }

    [RelayCommand]
    private void ValidateSharedFolder()
    {
        var items = new ObservableCollection<FolderValidationItem>();
        foreach (var subfolder in FolderNames.RequiredSubfolders)
        {
            var fullPath = string.IsNullOrWhiteSpace(SharedFolderPath)
                ? string.Empty
                : Path.Combine(SharedFolderPath, subfolder);
            items.Add(new FolderValidationItem
            {
                FolderName = subfolder,
                Exists = !string.IsNullOrWhiteSpace(fullPath) && Directory.Exists(fullPath)
            });
        }
        FolderValidationItems = items;
    }

    // OCR Region methods
    [RelayCommand]
    private void LoadOcrImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(dialog.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                FrameImage = bitmap;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load image");
                OcrTestResult = $"Failed to load image: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task CaptureOcrFrameAsync()
    {
        try
        {
            // Get the current video source's MediaPlayer
            var source = _sourcePanelViewModel?.CurrentSource;
            if (source is null || !source.IsConnected)
            {
                OcrTestResult = "No video source connected. Connect a source first, then capture.";
                return;
            }

            LibVLCSharp.Shared.MediaPlayer? mediaPlayer = source switch
            {
                LocalFileSource local => local.MediaPlayer,
                YouTubeSource yt => yt.MediaPlayer,
                LiveFeedSource live => live.MediaPlayer,
                _ => null
            };

            if (mediaPlayer is null || !mediaPlayer.IsPlaying)
            {
                OcrTestResult = "Video is not playing. Start playback first.";
                return;
            }

            OcrTestResult = "Capturing frame...";

            // Take snapshot via LibVLC
            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            Directory.CreateDirectory(tempDir);
            var snapshotPath = Path.Combine(tempDir, $"ocr_frame_{Guid.NewGuid():N}.png");

            mediaPlayer.TakeSnapshot(0, snapshotPath, 0, 0);
            await Task.Delay(800); // Wait for file write

            if (File.Exists(snapshotPath) && new FileInfo(snapshotPath).Length > 0)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(snapshotPath);
                bitmap.EndInit();
                bitmap.Freeze();

                FrameImage = bitmap;
                OcrTestResult = $"Frame captured ({bitmap.PixelWidth}x{bitmap.PixelHeight}). Draw a rectangle over the text area.";

                // Cleanup temp file
                try { File.Delete(snapshotPath); } catch { }
            }
            else
            {
                OcrTestResult = "Failed to capture frame. Ensure video is playing.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Frame capture failed");
            OcrTestResult = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveOcrRegionAsync()
    {
        if (OcrCropWidth <= 0 || OcrCropHeight <= 0)
        {
            OcrTestResult = "Draw a rectangle on the image first to define the OCR region.";
            return;
        }

        try
        {
            _settingsService.UpdateSettings(s =>
            {
                s.OcrCropX = OcrCropX;
                s.OcrCropY = OcrCropY;
                s.OcrCropWidth = OcrCropWidth;
                s.OcrCropHeight = OcrCropHeight;
            });
            await _settingsService.SaveAsync();

            OcrTestResult = $"OCR region saved (X:{OcrCropX}, Y:{OcrCropY}, W:{OcrCropWidth}, H:{OcrCropHeight}). The 'Capture Text' button will use this region.";
            Log.Information("OCR region saved: X={X}, Y={Y}, W={W}, H={H}", OcrCropX, OcrCropY, OcrCropWidth, OcrCropHeight);
        }
        catch (Exception ex)
        {
            OcrTestResult = $"Save failed: {ex.Message}";
            Log.Error(ex, "Failed to save OCR region");
        }
    }

    [RelayCommand]
    private async Task ClearOcrRegionAsync()
    {
        OcrCropX = 0;
        OcrCropY = 0;
        OcrCropWidth = 0;
        OcrCropHeight = 0;

        _settingsService.UpdateSettings(s =>
        {
            s.OcrCropX = 0;
            s.OcrCropY = 0;
            s.OcrCropWidth = 0;
            s.OcrCropHeight = 0;
        });
        await _settingsService.SaveAsync();

        OcrTestResult = "OCR region cleared.";
    }

    [RelayCommand]
    private async Task SaveOcrProfileAsync()
    {
        // Legacy — kept for compatibility but now SaveOcrRegion is primary
        await SaveOcrRegionAsync();
    }

    [RelayCommand]
    private async Task DeleteOcrProfileAsync()
    {
        if (_ocrProfileProvider is null || string.IsNullOrWhiteSpace(OcrProfileName)) return;

        try
        {
            await _ocrProfileProvider.DeleteProfileAsync(OcrProfileName);
            OcrTestResult = "Profile deleted";
            OcrProfileName = string.Empty;
            await LoadOcrProfilesAsync();
        }
        catch (Exception ex)
        {
            OcrTestResult = $"Delete failed: {ex.Message}";
            Log.Error(ex, "Failed to delete OCR profile");
        }
    }

    [RelayCommand]
    private async Task TestOcrAsync()
    {
        if (FrameImage is null)
        {
            OcrTestResult = "Load an image or capture a frame first.";
            return;
        }

        if (OcrCropWidth <= 0 || OcrCropHeight <= 0)
        {
            OcrTestResult = "Draw a rectangle on the image to define the OCR region first.";
            return;
        }

        try
        {
            OcrTestResult = "Running OCR (EasyOCR + Tesseract)...";

            var result = await Task.Run(async () =>
            {
                // Ensure minimum 100px height for reliable CRAFT text detection
                var userCropH = OcrCropHeight;
                var userCropY = OcrCropY;
                const int minH = 100;
                if (userCropH < minH)
                {
                    var extra = minH - userCropH;
                    userCropY = Math.Max(0, userCropY - extra / 2);
                    userCropH = minH;
                }
                var cropX = Math.Max(0, Math.Min(OcrCropX, FrameImage.PixelWidth - 1));
                var cropY = Math.Max(0, Math.Min(userCropY, FrameImage.PixelHeight - 1));
                var cropW = Math.Min(OcrCropWidth, FrameImage.PixelWidth - cropX);
                var cropH = Math.Min(userCropH, FrameImage.PixelHeight - cropY);

                if (cropW <= 0 || cropH <= 0) return "Crop region is out of image bounds.";

                var cropped = new System.Windows.Media.Imaging.CroppedBitmap(
                    FrameImage, new System.Windows.Int32Rect(cropX, cropY, cropW, cropH));

                // Save cropped image to temp file
                var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                Directory.CreateDirectory(tempDir);
                var tempPath = Path.Combine(tempDir, $"ocr_test_{Guid.NewGuid():N}.png");

                using (var fs = new FileStream(tempPath, FileMode.Create))
                {
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(cropped));
                    encoder.Save(fs);
                }

                try
                {
                    // Try EasyOCR first (best Bengali accuracy)
                    var ocrScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "ocr", "ocr_engine.py");
                    if (File.Exists(ocrScript))
                    {
                        var pythonExe = FindPythonExe();
                        if (pythonExe is not null)
                        {
                            var proc = new System.Diagnostics.Process
                            {
                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = pythonExe,
                                    Arguments = $"\"{ocrScript}\" \"{tempPath}\"",
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                                    StandardErrorEncoding = System.Text.Encoding.UTF8
                                }
                            };
                            proc.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                            proc.Start();

                            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                            var stderrTask = proc.StandardError.ReadToEndAsync();
                            // EasyOCR first load can take 60+ seconds
                            proc.WaitForExit(120000);
                            var stdout = await stdoutTask;
                            var stderr = await stderrTask;

                            if (!string.IsNullOrEmpty(stderr))
                                Log.Debug("EasyOCR stderr: {Stderr}", stderr.Length > 200 ? stderr[..200] : stderr);

                            if (!string.IsNullOrWhiteSpace(stdout))
                            {
                                var textParts = new List<string>();
                                foreach (var line in stdout.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                {
                                    var parts = line.Split('|', 2);
                                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                                        textParts.Add(parts[1].Trim());
                                }
                                if (textParts.Count > 0)
                                {
                                    var easyText = string.Join(" ", textParts);
                                    return $"EasyOCR Detected:\n{easyText}";
                                }
                            }
                            // If EasyOCR returned nothing, log it
                            Log.Warning("EasyOCR returned empty. Exit: {Code}", proc.ExitCode);
                        }
                        else
                        {
                            Log.Warning("Python not found for EasyOCR");
                        }
                    }
                    else
                    {
                        Log.Warning("EasyOCR script not found at: {Path}", ocrScript);
                    }

                    // Fallback: Tesseract
                    var tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "tessdata");
                    var hasBen = File.Exists(Path.Combine(tessdataPath, "ben.traineddata"));
                    var hasEng = File.Exists(Path.Combine(tessdataPath, "eng.traineddata"));
                    // Use ben+eng combined for mixed text, fallback to single
                    var lang = hasBen && hasEng ? "ben+eng" : (hasBen ? "ben" : "eng");

                    using var engine = new Tesseract.TesseractEngine(tessdataPath, lang, Tesseract.EngineMode.LstmOnly);
                    // Set DPI for better recognition of screen-captured text
                    engine.SetVariable("user_defined_dpi", "300");

                    using var rawImg = Tesseract.Pix.LoadFromFile(tempPath);

                    // Prepare multiple image variants for best result
                    var bestText = string.Empty;
                    var bestConfidence = 0f;
                    var allResults = new System.Text.StringBuilder();

                    // Variant 1: Raw color image scaled 4x
                    using (var scaled = rawImg.Scale(4.0f, 4.0f))
                    {
                        TryOcr(engine, scaled, Tesseract.PageSegMode.SingleBlock, ref bestText, ref bestConfidence);
                        TryOcr(engine, scaled, Tesseract.PageSegMode.SingleLine, ref bestText, ref bestConfidence);
                    }

                    // Variant 2: Grayscale → scale 4x → binarize
                    using (var gray = rawImg.ConvertRGBToGray())
                    using (var scaled = gray.Scale(4.0f, 4.0f))
                    {
                        TryOcr(engine, scaled, Tesseract.PageSegMode.SingleBlock, ref bestText, ref bestConfidence);

                        var bin = scaled.BinarizeOtsuAdaptiveThreshold(2000, 2000, 0, 0, 0.0f);
                        if (bin is not null)
                        {
                            using (bin)
                            {
                                TryOcr(engine, bin, Tesseract.PageSegMode.SingleBlock, ref bestText, ref bestConfidence);
                                // Also try inverted (for light text on dark bg)
                                using var inverted = bin.Invert();
                                TryOcr(engine, inverted, Tesseract.PageSegMode.SingleBlock, ref bestText, ref bestConfidence);
                            }
                        }
                    }

                    // Variant 3: Raw image without scaling
                    TryOcr(engine, rawImg, Tesseract.PageSegMode.SingleBlock, ref bestText, ref bestConfidence);
                    TryOcr(engine, rawImg, Tesseract.PageSegMode.Auto, ref bestText, ref bestConfidence);

                    var confidence = bestConfidence * 100;

                    if (string.IsNullOrWhiteSpace(bestText))
                        return $"No text detected (Confidence: {confidence:F0}%).\nCrop: {cropW}x{cropH}px\nTry selecting the text area more tightly.";

                    // Clean: take only the first meaningful line (headline is always one line)
                    var lines = bestText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 3)
                        .ToList();
                    var cleanText = lines.Count > 0 ? lines[0] : bestText.Trim();

                    return $"Detected ({confidence:F0}%):\n{cleanText}";
                }
                finally
                {
                    try { File.Delete(tempPath); } catch { }
                }
            });

            OcrTestResult = result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OCR test failed");
            OcrTestResult = $"OCR failed: {ex.Message}";
        }
    }

    private static string? FindPythonExe()
    {
        // Bundled portable Python first
        var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "python", "python.exe");
        if (File.Exists(bundled)) return bundled;

        // System Python fallback
        foreach (var cmd in new[] { "python", "python3", "py" })
        {
            try
            {
                var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = cmd, Arguments = "--version",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                });
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0) return cmd;
            }
            catch { }
        }
        return null;
    }

    private static void TryOcr(Tesseract.TesseractEngine engine, Tesseract.Pix img,
        Tesseract.PageSegMode psm, ref string bestText, ref float bestConfidence)
    {
        try
        {
            using var page = engine.Process(img, psm);
            var text = page.GetText()?.Trim() ?? string.Empty;
            var conf = page.GetMeanConfidence();

            // Pick result with most actual text content (not just highest confidence)
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Prefer longer text if confidence is reasonable (> 40%)
                if (conf > 0.4f && text.Length > bestText.Length)
                {
                    bestText = text;
                    bestConfidence = conf;
                }
                else if (conf > bestConfidence && text.Length >= bestText.Length * 0.8)
                {
                    bestText = text;
                    bestConfidence = conf;
                }
            }
        }
        catch { /* skip failed attempt */ }
    }

    public async Task LoadOcrProfilesAsync()
    {
        if (_ocrProfileProvider is null) return;

        try
        {
            var profiles = await _ocrProfileProvider.GetProfilesAsync();
            AvailableProfiles = new ObservableCollection<OcrProfile>(profiles);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load OCR profiles");
        }
    }

    // ── Dependencies ────────────────────────────────────────────────
    private static readonly string AppBase = AppDomain.CurrentDomain.BaseDirectory;
    private CancellationTokenSource? _downloadCts;

    public ObservableCollection<DependencyItem> Dependencies { get; } = new();

    private void InitializeDependencies()
    {
        Dependencies.Clear();
        Dependencies.Add(new DependencyItem
        {
            Name = "FFmpeg",
            Description = "Audio/video recording & processing",
            CheckPath = Path.Combine(AppBase, "lib", "ffmpeg", "ffmpeg.exe"),
            DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            DestinationFolder = Path.Combine(AppBase, "lib", "ffmpeg"),
            IsZip = true
        });
        Dependencies.Add(new DependencyItem
        {
            Name = "yt-dlp",
            Description = "YouTube stream extraction",
            CheckPath = Path.Combine(AppBase, "lib", "yt-dlp", "yt-dlp.exe"),
            DownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
            DestinationFolder = Path.Combine(AppBase, "lib", "yt-dlp"),
            IsZip = false
        });
        Dependencies.Add(new DependencyItem
        {
            Name = "Tesseract Data (Bengali)",
            Description = "OCR trained data for Bengali text",
            CheckPath = Path.Combine(AppBase, "lib", "tessdata", "ben.traineddata"),
            DownloadUrl = "https://github.com/tesseract-ocr/tessdata_best/raw/main/ben.traineddata",
            DestinationFolder = Path.Combine(AppBase, "lib", "tessdata"),
            IsZip = false
        });
        Dependencies.Add(new DependencyItem
        {
            Name = "Tesseract Data (English)",
            Description = "OCR trained data for English text",
            CheckPath = Path.Combine(AppBase, "lib", "tessdata", "eng.traineddata"),
            DownloadUrl = "https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata",
            DestinationFolder = Path.Combine(AppBase, "lib", "tessdata"),
            IsZip = false
        });
        Dependencies.Add(new DependencyItem
        {
            Name = "Python + EasyOCR",
            Description = "EasyOCR engine for Bengali. May take several minutes.",
            CheckPath = Path.Combine(AppBase, "lib", "python", "python.exe"),
            SecondaryCheckPath = Path.Combine(AppBase, "lib", "python", "Lib", "site-packages", "easyocr"),
            DownloadUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip",
            DestinationFolder = Path.Combine(AppBase, "lib", "python"),
            IsZip = true,
            IsPythonSetup = true
        });
    }

    [RelayCommand]
    private async Task CheckDependenciesAsync()
    {
        if (Dependencies.Count == 0)
            InitializeDependencies();

        await Task.Run(() =>
        {
            foreach (var dep in Dependencies)
            {
                var primaryExists = File.Exists(dep.CheckPath) || Directory.Exists(dep.CheckPath);
                var secondaryOk = string.IsNullOrEmpty(dep.SecondaryCheckPath)
                    || File.Exists(dep.SecondaryCheckPath)
                    || Directory.Exists(dep.SecondaryCheckPath);

                dep.IsInstalled = primaryExists && secondaryOk;
                dep.Status = dep.IsInstalled ? "Installed" : "Missing";
            }
        });
    }

    [RelayCommand]
    private async Task DownloadDependencyAsync(DependencyItem? item)
    {
        if (item is null || item.IsDownloading) return;

        try
        {
            item.IsDownloading = true;
            item.DownloadProgress = 0;
            item.DownloadSpeed = "";
            item.Eta = "";
            _downloadCts = new CancellationTokenSource();
            var ct = _downloadCts.Token;

            Directory.CreateDirectory(item.DestinationFolder);

            if (item.IsPythonSetup)
            {
                await SetupPythonEasyOcrAsync(item, ct);
            }
            else if (item.IsZip)
            {
                var tempZip = Path.Combine(Path.GetTempPath(), $"onaircut_{item.Name.Replace(" ", "_")}_{Guid.NewGuid():N}.zip");
                try
                {
                    await DownloadFileWithProgressAsync(item, item.DownloadUrl, tempZip, ct);
                    item.Status = "Extracting...";
                    await ExtractZipAsync(item, tempZip, item.DestinationFolder, ct);
                }
                finally
                {
                    try { File.Delete(tempZip); } catch { }
                }
            }
            else
            {
                var fileName = Path.GetFileName(new Uri(item.DownloadUrl).LocalPath);
                var destPath = Path.Combine(item.DestinationFolder, fileName);
                await DownloadFileWithProgressAsync(item, item.DownloadUrl, destPath, ct);
            }

            // Re-check status
            var primaryExists = File.Exists(item.CheckPath) || Directory.Exists(item.CheckPath);
            var secondaryOk = string.IsNullOrEmpty(item.SecondaryCheckPath)
                || File.Exists(item.SecondaryCheckPath)
                || Directory.Exists(item.SecondaryCheckPath);
            item.IsInstalled = primaryExists && secondaryOk;
            item.Status = item.IsInstalled ? "Installed" : "Download may have failed";
            item.DownloadProgress = item.IsInstalled ? 100 : 0;

            Log.Information("Dependency download completed: {Name}, Installed: {Installed}", item.Name, item.IsInstalled);
        }
        catch (OperationCanceledException)
        {
            item.Status = "Cancelled";
            Log.Information("Dependency download cancelled: {Name}", item.Name);
        }
        catch (Exception ex)
        {
            item.Status = $"Error: {ex.Message}";
            Log.Error(ex, "Failed to download dependency: {Name}", item.Name);
        }
        finally
        {
            item.IsDownloading = false;
            item.DownloadSpeed = "";
            item.Eta = "";
        }
    }

    [RelayCommand]
    private async Task DownloadAllMissingAsync()
    {
        if (Dependencies.Count == 0)
            await CheckDependenciesAsync();

        foreach (var dep in Dependencies.Where(d => !d.IsInstalled && !d.IsDownloading))
        {
            await DownloadDependencyAsync(dep);
        }
    }

    private async Task DownloadFileWithProgressAsync(DependencyItem item, string url, string destPath, CancellationToken ct)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(30);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        if (totalBytes > 0)
            item.Size = $"{totalBytes / 1024.0 / 1024.0:F1} MB";

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long downloaded = 0;
        var sw = Stopwatch.StartNew();
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloaded += bytesRead;

            var progress = totalBytes > 0 ? (double)downloaded / totalBytes * 100 : 0;
            item.DownloadProgress = progress;

            if (sw.Elapsed.TotalSeconds > 0.5)
            {
                var speed = downloaded / sw.Elapsed.TotalSeconds;
                item.DownloadSpeed = $"{speed / 1024.0 / 1024.0:F1} MB/s";

                if (speed > 0 && totalBytes > 0)
                {
                    var remaining = (totalBytes - downloaded) / speed;
                    item.Eta = $"ETA: {TimeSpan.FromSeconds(remaining):mm\\:ss}";
                }
            }
        }
    }

    private static async Task ExtractZipAsync(DependencyItem item, string zipPath, string destFolder, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);

            // For FFmpeg, extract only the bin/*.exe files from the nested folder
            if (item.Name == "FFmpeg")
            {
                foreach (var entry in archive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    // Entries look like: ffmpeg-7.0-essentials_build/bin/ffmpeg.exe
                    if (entry.FullName.Contains("/bin/") && entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var destPath = Path.Combine(destFolder, entry.Name);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }
            }
            else
            {
                // Generic: extract all to destination
                ZipFile.ExtractToDirectory(zipPath, destFolder, overwriteFiles: true);
            }
        }, ct);
    }

    private async Task SetupPythonEasyOcrAsync(DependencyItem item, CancellationToken ct)
    {
        var pythonDir = item.DestinationFolder;
        var pythonExe = Path.Combine(pythonDir, "python.exe");

        // Step 1: Download and extract Python embeddable
        if (!File.Exists(pythonExe))
        {
            item.Status = "Downloading Python...";
            var tempZip = Path.Combine(Path.GetTempPath(), $"python_embed_{Guid.NewGuid():N}.zip");
            try
            {
                await DownloadFileWithProgressAsync(item, item.DownloadUrl, tempZip, ct);
                item.Status = "Extracting Python...";
                Directory.CreateDirectory(pythonDir);
                await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, pythonDir, overwriteFiles: true), ct);
            }
            finally
            {
                try { File.Delete(tempZip); } catch { }
            }
        }

        // Step 2: Enable site-packages by modifying python311._pth
        item.Status = "Configuring Python...";
        var pthFiles = Directory.GetFiles(pythonDir, "python*._pth");
        foreach (var pthFile in pthFiles)
        {
            var content = await File.ReadAllTextAsync(pthFile, ct);
            if (content.Contains("#import site"))
            {
                content = content.Replace("#import site", "import site");
                await File.WriteAllTextAsync(pthFile, content, ct);
            }
        }

        // Step 3: Download and run get-pip.py
        var pipExe = Path.Combine(pythonDir, "Scripts", "pip.exe");
        if (!File.Exists(pipExe))
        {
            item.Status = "Installing pip...";
            var getPipPath = Path.Combine(pythonDir, "get-pip.py");
            await DownloadFileWithProgressAsync(item,
                "https://bootstrap.pypa.io/get-pip.py", getPipPath, ct);

            var pipProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{getPipPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = pythonDir
                }
            };
            pipProc.Start();
            await pipProc.WaitForExitAsync(ct);

            try { File.Delete(getPipPath); } catch { }
        }

        // Step 4: Install EasyOCR with real-time progress from pip output
        item.Status = "Step 4/5: Installing EasyOCR packages...";
        item.DownloadProgress = 40;
        item.DownloadSpeed = "";
        item.Eta = "This takes 5-10 min";

        var installProc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = "-m pip install easyocr --no-warn-script-location --progress-bar ascii",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = pythonDir
            }
        };
        installProc.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        installProc.Start();

        // Read pip output line by line to show progress
        var pipOutputTask = Task.Run(async () =>
        {
            var packageCount = 0;
            while (!installProc.StandardOutput.EndOfStream)
            {
                var line = await installProc.StandardOutput.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;

                if (line.Contains("Collecting"))
                {
                    var pkg = line.Replace("Collecting ", "").Split(' ')[0];
                    item.Status = $"Step 4/5: Downloading {pkg}...";
                }
                else if (line.Contains("Downloading"))
                {
                    // Extract package name and size from pip download line
                    var parts = line.Trim();
                    item.Status = $"Step 4/5: {parts}";
                    item.DownloadProgress = Math.Min(40 + packageCount * 2, 75);
                }
                else if (line.Contains("Installing collected"))
                {
                    item.Status = "Step 4/5: Installing packages locally...";
                    item.DownloadProgress = 78;
                }
                else if (line.Contains("Successfully installed"))
                {
                    packageCount = line.Split(' ').Length - 2;
                    item.Status = $"Step 4/5: Installed {packageCount} packages";
                    item.DownloadProgress = 80;
                }
            }
        }, ct);

        var stderrTask = installProc.StandardError.ReadToEndAsync(ct);
        await installProc.WaitForExitAsync(ct);
        await pipOutputTask;
        var stderr = await stderrTask;

        if (installProc.ExitCode != 0)
        {
            Log.Warning("EasyOCR pip install stderr: {Stderr}", stderr.Length > 500 ? stderr[..500] : stderr);
            item.Status = "EasyOCR install failed! Check logs.";
            item.IsDownloading = false;
            return;
        }

        // Step 5: Download Bengali OCR models
        item.Status = "Step 5/5: Downloading Bengali OCR model (205 MB)...";
        item.DownloadProgress = 82;
        item.Eta = "";
        item.DownloadSpeed = "";

        var ocrModelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "ocr", "models");
        Directory.CreateDirectory(ocrModelsDir);
        var bengaliModelPath = Path.Combine(ocrModelsDir, "bengali.pth");

        if (!File.Exists(bengaliModelPath))
        {
            // Use EasyOCR to download models automatically
            var modelProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"-c \"import easyocr,os; r=easyocr.Reader(['bn','en'],gpu=False,model_storage_directory=r'{ocrModelsDir}',verbose=False); print('OK')\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = pythonDir
                }
            };
            modelProc.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

            item.Status = "Step 5/5: Downloading Bengali model (this takes 2-3 min)...";
            item.DownloadProgress = 85;
            item.Eta = "~2-3 min";

            modelProc.Start();

            // Animate progress while waiting
            var animTask = Task.Run(async () =>
            {
                var progress = 85.0;
                while (!modelProc.HasExited && progress < 98)
                {
                    await Task.Delay(3000, ct);
                    progress += 0.5;
                    item.DownloadProgress = progress;
                    item.Status = $"Step 5/5: Downloading Bengali model... {progress:F0}%";
                }
            }, ct);

            await modelProc.WaitForExitAsync(ct);
            await animTask;
        }

        item.DownloadProgress = 100;
        item.Eta = "";
        item.DownloadSpeed = "";
        item.Status = "All installed!";
    }
}
