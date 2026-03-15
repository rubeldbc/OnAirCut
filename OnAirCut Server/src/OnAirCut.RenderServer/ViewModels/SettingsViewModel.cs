using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Utilities;
using OnAirCut.RenderServer.Helpers;
using OnAirCut.RenderServer.Models;
using OnAirCut.RenderServer.Services;
using Serilog;

namespace OnAirCut.RenderServer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ISharedFolderService _sharedFolderService;

    // Paths
    [ObservableProperty] private string _sharedFolderPath = string.Empty;
    [ObservableProperty] private string _ffmpegPath = "ffmpeg";
    [ObservableProperty] private string _ffprobePath = "ffprobe";
    [ObservableProperty] private string _ocrEnginePath = string.Empty;
    [ObservableProperty] private string _localDatabasePath = @"C:\OnAirCut\Data\onaircut.db";
    [ObservableProperty] private string _tempWorkingFolder = string.Empty;

    // Rendering
    [ObservableProperty] private string _outputVideoCodec = "libx264";
    [ObservableProperty] private string _outputVideoPreset = "fast";
    [ObservableProperty] private int _outputVideoCRF = 18;
    [ObservableProperty] private string _outputAudioCodec = "aac";
    [ObservableProperty] private string _outputAudioBitrate = "192k";

    // OCR
    [ObservableProperty] private string _ocrLanguage = "ben";
    [ObservableProperty] private int _ocrMultiFrameCount = 5;
    [ObservableProperty] private int _frameExtractionCount = 20;

    // Queue
    [ObservableProperty] private int _maxConcurrentRenders = 1;
    [ObservableProperty] private int _jobPollIntervalMs = 2000;
    [ObservableProperty] private int _fileReadyCheckIntervalMs = 1000;
    [ObservableProperty] private int _fileReadyStableSeconds = 3;

    // API
    [ObservableProperty] private int _apiPort = 5123;
    [ObservableProperty] private bool _apiEnabled = true;

    // Cleanup
    [ObservableProperty] private int _cleanupWorkingFolderAfterDays = 7;

    // UI state
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isSaving;

    // Shared Folder Validation
    [ObservableProperty] private ObservableCollection<FolderValidationItem> _folderValidationItems = [];

    public List<string> VideoCodecOptions { get; } = ["libx264", "libx265", "libvpx-vp9", "h264_nvenc", "hevc_nvenc"];
    public List<string> PresetOptions { get; } = ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"];
    public List<string> AudioCodecOptions { get; } = ["aac", "libmp3lame", "libopus", "copy"];

    public SettingsViewModel(ISettingsService settingsService, ISharedFolderService sharedFolderService)
    {
        _settingsService = settingsService;
        _sharedFolderService = sharedFolderService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;
        SharedFolderPath = s.SharedFolderPath;
        FfmpegPath = s.FFmpegPath;
        FfprobePath = s.FFprobePath;
        OcrEnginePath = s.OcrEnginePath;
        LocalDatabasePath = s.LocalDatabasePath;
        TempWorkingFolder = s.TempWorkingFolder;
        OutputVideoCodec = s.OutputVideoCodec;
        OutputVideoPreset = s.OutputVideoPreset;
        OutputVideoCRF = s.OutputVideoCRF;
        OutputAudioCodec = s.OutputAudioCodec;
        OutputAudioBitrate = s.OutputAudioBitrate;
        OcrLanguage = s.OcrLanguage;
        OcrMultiFrameCount = s.OcrMultiFrameCount;
        FrameExtractionCount = s.FrameExtractionCount;
        MaxConcurrentRenders = s.MaxConcurrentRenders;
        JobPollIntervalMs = s.JobPollIntervalMs;
        FileReadyCheckIntervalMs = s.FileReadyCheckIntervalMs;
        FileReadyStableSeconds = s.FileReadyStableSeconds;
        ApiPort = s.ApiPort;
        ApiEnabled = s.ApiEnabled;
        CleanupWorkingFolderAfterDays = s.CleanupWorkingFolderAfterDays;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        try
        {
            var s = _settingsService.Settings;
            s.SharedFolderPath = SharedFolderPath;
            s.FFmpegPath = FfmpegPath;
            s.FFprobePath = FfprobePath;
            s.OcrEnginePath = OcrEnginePath;
            s.LocalDatabasePath = LocalDatabasePath;
            s.TempWorkingFolder = TempWorkingFolder;
            s.OutputVideoCodec = OutputVideoCodec;
            s.OutputVideoPreset = OutputVideoPreset;
            s.OutputVideoCRF = OutputVideoCRF;
            s.OutputAudioCodec = OutputAudioCodec;
            s.OutputAudioBitrate = OutputAudioBitrate;
            s.OcrLanguage = OcrLanguage;
            s.OcrMultiFrameCount = OcrMultiFrameCount;
            s.FrameExtractionCount = FrameExtractionCount;
            s.MaxConcurrentRenders = MaxConcurrentRenders;
            s.JobPollIntervalMs = JobPollIntervalMs;
            s.FileReadyCheckIntervalMs = FileReadyCheckIntervalMs;
            s.FileReadyStableSeconds = FileReadyStableSeconds;
            s.ApiPort = ApiPort;
            s.ApiEnabled = ApiEnabled;
            s.CleanupWorkingFolderAfterDays = CleanupWorkingFolderAfterDays;

            await _settingsService.SaveAsync();
            StatusMessage = "Settings saved successfully!";
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
        var s = new RenderServerSettings();
        SharedFolderPath = s.SharedFolderPath;
        FfmpegPath = s.FFmpegPath;
        FfprobePath = s.FFprobePath;
        OcrEnginePath = s.OcrEnginePath;
        LocalDatabasePath = s.LocalDatabasePath;
        TempWorkingFolder = s.TempWorkingFolder;
        OutputVideoCodec = s.OutputVideoCodec;
        OutputVideoPreset = s.OutputVideoPreset;
        OutputVideoCRF = s.OutputVideoCRF;
        OutputAudioCodec = s.OutputAudioCodec;
        OutputAudioBitrate = s.OutputAudioBitrate;
        OcrLanguage = s.OcrLanguage;
        OcrMultiFrameCount = s.OcrMultiFrameCount;
        FrameExtractionCount = s.FrameExtractionCount;
        MaxConcurrentRenders = s.MaxConcurrentRenders;
        JobPollIntervalMs = s.JobPollIntervalMs;
        FileReadyCheckIntervalMs = s.FileReadyCheckIntervalMs;
        FileReadyStableSeconds = s.FileReadyStableSeconds;
        ApiPort = s.ApiPort;
        ApiEnabled = s.ApiEnabled;
        CleanupWorkingFolderAfterDays = s.CleanupWorkingFolderAfterDays;
        StatusMessage = "Settings reset to defaults";
    }

    [RelayCommand]
    private void BrowseSharedFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Shared Folder" };
        if (dialog.ShowDialog() == true)
            SharedFolderPath = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseFFmpeg()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select FFmpeg executable",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            FfmpegPath = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseFFprobe()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select FFprobe executable",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            FfprobePath = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseOcrEngine()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Tesseract Data Directory" };
        if (dialog.ShowDialog() == true)
            OcrEnginePath = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseDatabase()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select Database Location",
            Filter = "SQLite database (*.db)|*.db",
            DefaultExt = ".db"
        };
        if (dialog.ShowDialog() == true)
            LocalDatabasePath = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseTempFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Temp Working Folder" };
        if (dialog.ShowDialog() == true)
            TempWorkingFolder = dialog.FolderName;
    }

    [RelayCommand]
    private async Task TestFFmpegAsync()
    {
        var available = await ProcessHelper.IsExecutableAvailableAsync(FfmpegPath);
        StatusMessage = available ? "FFmpeg is available and working!" : "FFmpeg not found or not working.";
    }

    [RelayCommand]
    private async Task TestOcrAsync()
    {
        if (string.IsNullOrEmpty(OcrEnginePath) || !Directory.Exists(OcrEnginePath))
        {
            StatusMessage = "OCR engine path not set or directory doesn't exist.";
            return;
        }
        StatusMessage = $"OCR data path exists: {OcrEnginePath}";
        await Task.CompletedTask;
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
            Description = "Audio/video rendering & processing",
            CheckPath = Path.Combine(AppBase, "lib", "ffmpeg", "ffmpeg.exe"),
            SecondaryCheckPath = Path.Combine(AppBase, "lib", "ffmpeg", "ffprobe.exe"),
            DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            DestinationFolder = Path.Combine(AppBase, "lib", "ffmpeg"),
            IsZip = true
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
        Dependencies.Add(new DependencyItem
        {
            Name = "EasyOCR Bengali Models",
            Description = "Pre-trained models for Bengali text recognition",
            CheckPath = Path.Combine(AppBase, "lib", "ocr", "models", "bengali.pth"),
            SecondaryCheckPath = Path.Combine(AppBase, "lib", "ocr", "models", "craft_mlt_25k.pth"),
            DownloadUrl = "",
            DestinationFolder = Path.Combine(AppBase, "lib", "ocr", "models"),
            IsZip = false
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

        // EasyOCR Bengali Models are auto-downloaded by EasyOCR on first run
        if (item.Name == "EasyOCR Bengali Models" && string.IsNullOrEmpty(item.DownloadUrl))
        {
            item.Status = "Auto-downloaded by EasyOCR on first run";
            return;
        }

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

        // Step 4: Install EasyOCR
        item.Status = "Installing EasyOCR (this may take several minutes)...";
        item.DownloadProgress = 50;
        var installProc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = "-m pip install easyocr",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = pythonDir
            }
        };
        installProc.Start();
        await installProc.WaitForExitAsync(ct);
        var stderr = await installProc.StandardError.ReadToEndAsync(ct);

        if (installProc.ExitCode != 0)
        {
            Log.Warning("EasyOCR pip install stderr: {Stderr}", stderr.Length > 500 ? stderr[..500] : stderr);
        }

        item.DownloadProgress = 100;
        item.Status = "Setup complete";
    }
}
