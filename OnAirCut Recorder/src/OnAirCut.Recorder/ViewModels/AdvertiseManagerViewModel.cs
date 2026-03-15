using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using OnAirCut.Recorder.Services;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class AdvertiseManagerViewModel : ObservableObject
{
    private readonly IAdSetProvider _adSetProvider;
    private readonly ISettingsService _settingsService;
    private readonly SourcePanelViewModel _sourcePanelViewModel;

    private static readonly JsonSerializerOptions JsonDisplayOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string BgImagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OnAirCut", "bg.png");

    public AdvertiseManagerViewModel(
        IAdSetProvider adSetProvider,
        ISettingsService settingsService,
        SourcePanelViewModel sourcePanelViewModel)
    {
        _adSetProvider = adSetProvider;
        _settingsService = settingsService;
        _sourcePanelViewModel = sourcePanelViewModel;
        _adSetProvider.AdSetsChanged += async (_, _) => await LoadAdSetListAsync();
        _adPanelWidth = settingsService.Settings.AdPanelWidth;
    }

    // --- Ad Set Browser ---
    [ObservableProperty] private ObservableCollection<string> _adSetNames = [];
    [ObservableProperty] private string? _selectedAdSetName;
    [ObservableProperty] private AdSetConfig? _currentConfig;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // --- Panel width (persisted) ---
    [ObservableProperty] private double _adPanelWidth = 300;
    partial void OnAdPanelWidthChanged(double value) =>
        _settingsService.UpdateSettings(s => s.AdPanelWidth = value);

    // --- Accordion ---
    [ObservableProperty] private int _expandedIndex = -1;

    // --- Background Frame ---
    [ObservableProperty] private BitmapImage? _backgroundFrame;

    // =================== Doggy (Floating Logo) ===================
    [ObservableProperty] private bool _doggyEnabled;
    [ObservableProperty] private string? _doggyFile;
    [ObservableProperty] private string? _doggyFileFullPath;
    partial void OnDoggyFileChanged(string? value) => ResolveFilePath(value, v => DoggyFileFullPath = v);
    [ObservableProperty] private double _doggyStartFrom;
    [ObservableProperty] private double _doggyPositionX;
    [ObservableProperty] private double _doggyPositionY;
    [ObservableProperty] private double _doggyWidth = 200;
    [ObservableProperty] private double _doggyHeight = 150;
    [ObservableProperty] private double _doggyCropTop;
    [ObservableProperty] private double _doggyCropRight;
    [ObservableProperty] private double _doggyCropBottom;
    [ObservableProperty] private double _doggyCropLeft;
    [ObservableProperty] private double _doggyOpacity = 1.0;

    // =================== Popup ===================
    [ObservableProperty] private bool _popupEnabled;
    [ObservableProperty] private string? _popupFile;
    [ObservableProperty] private string? _popupFileFullPath;
    partial void OnPopupFileChanged(string? value) => ResolveFilePath(value, v => PopupFileFullPath = v);
    [ObservableProperty] private double _popupStartFrom;
    [ObservableProperty] private double _popupDurationPerTime = 5.0;
    [ObservableProperty] private int _popupTotalPlay = 1;
    [ObservableProperty] private double _popupPositionX;
    [ObservableProperty] private double _popupPositionY;
    [ObservableProperty] private double _popupWidth = 400;
    [ObservableProperty] private double _popupHeight = 300;
    [ObservableProperty] private double _popupCropTop;
    [ObservableProperty] private double _popupCropRight;
    [ObservableProperty] private double _popupCropBottom;
    [ObservableProperty] private double _popupCropLeft;
    [ObservableProperty] private double _popupOpacity = 1.0;

    // =================== TVC ===================
    [ObservableProperty] private bool _tvcEnabled;
    [ObservableProperty] private string? _tvcFile;
    [ObservableProperty] private int _tvcCount = 1;

    // --- Available files ---
    [ObservableProperty] private ObservableCollection<string> _availableFiles = [];

    // --- JSON display ---
    [ObservableProperty] private string _configJsonText = string.Empty;

    // ===================== Helpers =====================

    private void ResolveFilePath(string? fileName, Action<string?> setter)
    {
        if (string.IsNullOrEmpty(SelectedAdSetName) || string.IsNullOrEmpty(fileName))
        {
            setter(null);
            return;
        }
        var folder = _adSetProvider.GetAdSetFolderPath(SelectedAdSetName);
        var fullPath = Path.Combine(folder, fileName);
        setter(File.Exists(fullPath) ? fullPath : null);
    }

    // ===================== Init =====================

    public async Task InitializeAsync()
    {
        await LoadAdSetListAsync();
        LoadBackgroundImage();
    }

    private void LoadBackgroundImage()
    {
        try
        {
            if (File.Exists(BgImagePath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(BgImagePath);
                bitmap.EndInit();
                bitmap.Freeze();
                BackgroundFrame = bitmap;
            }
        }
        catch (Exception ex) { Log.Warning(ex, "Failed to load ad background image"); }
    }

    private void SaveBackgroundImage(BitmapImage bitmap)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BgImagePath)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = new FileStream(BgImagePath, FileMode.Create);
            encoder.Save(fs);
        }
        catch (Exception ex) { Log.Warning(ex, "Failed to save ad background image"); }
    }

    // ===================== Commands =====================

    [RelayCommand]
    private async Task LoadAdSetListAsync()
    {
        try
        {
            // Show ALL folders (with or without config.json) so user can configure any
            var allNames = await _adSetProvider.GetAllAdSetFolderNamesAsync();
            AdSetNames = new ObservableCollection<string>(allNames);
        }
        catch (Exception ex) { Log.Error(ex, "Failed to load ad set list"); }
    }

    async partial void OnSelectedAdSetNameChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        await LoadAdSetConfigAsync(value);
    }

    private async Task LoadAdSetConfigAsync(string adSetName)
    {
        try
        {
            var config = await _adSetProvider.GetAdSetByNameAsync(adSetName)
                         ?? new AdSetConfig { Name = adSetName };
            CurrentConfig = config;

            // Doggy
            DoggyEnabled = config.Doggy?.Enabled ?? false;
            DoggyFile = config.Doggy?.File;
            DoggyStartFrom = config.Doggy?.StartFrom ?? 0;
            DoggyPositionX = config.Doggy?.PositionX ?? 0;
            DoggyPositionY = config.Doggy?.PositionY ?? 0;
            DoggyWidth = config.Doggy?.Width ?? 200;
            DoggyHeight = config.Doggy?.Height ?? 150;
            DoggyCropTop = config.Doggy?.CropTop ?? 0;
            DoggyCropRight = config.Doggy?.CropRight ?? 0;
            DoggyCropBottom = config.Doggy?.CropBottom ?? 0;
            DoggyCropLeft = config.Doggy?.CropLeft ?? 0;
            DoggyOpacity = config.Doggy?.Opacity ?? 1.0;
            ResolveFilePath(DoggyFile, v => DoggyFileFullPath = v);

            // Popup
            PopupEnabled = config.Popup?.Enabled ?? false;
            PopupFile = config.Popup?.File;
            PopupStartFrom = config.Popup?.StartFrom ?? 0;
            PopupDurationPerTime = config.Popup?.DurationPerTime ?? 5.0;
            PopupTotalPlay = config.Popup?.TotalPlay ?? 1;
            PopupPositionX = config.Popup?.PositionX ?? 0;
            PopupPositionY = config.Popup?.PositionY ?? 0;
            PopupWidth = config.Popup?.Width ?? 400;
            PopupHeight = config.Popup?.Height ?? 300;
            PopupCropTop = config.Popup?.CropTop ?? 0;
            PopupCropRight = config.Popup?.CropRight ?? 0;
            PopupCropBottom = config.Popup?.CropBottom ?? 0;
            PopupCropLeft = config.Popup?.CropLeft ?? 0;
            PopupOpacity = config.Popup?.Opacity ?? 1.0;
            ResolveFilePath(PopupFile, v => PopupFileFullPath = v);

            // TVC
            TvcEnabled = config.Tvc?.Enabled ?? false;
            TvcFile = config.Tvc?.File;
            TvcCount = config.Tvc?.Count ?? 1;

            var files = await _adSetProvider.GetAvailableFilesAsync(adSetName);
            AvailableFiles = new ObservableCollection<string>(files);
            UpdateJsonDisplay();
            StatusMessage = $"Loaded: {adSetName}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load ad set config: {Name}", adSetName);
            StatusMessage = $"Error loading {adSetName}";
        }
    }

    private AdSetConfig BuildConfigFromFields() => new()
    {
        Name = SelectedAdSetName ?? string.Empty,
        Doggy = new DoggyAdConfig
        {
            Enabled = DoggyEnabled, File = DoggyFile, StartFrom = DoggyStartFrom,
            PositionX = DoggyPositionX, PositionY = DoggyPositionY,
            Width = DoggyWidth, Height = DoggyHeight,
            CropTop = DoggyCropTop, CropRight = DoggyCropRight,
            CropBottom = DoggyCropBottom, CropLeft = DoggyCropLeft,
            Opacity = DoggyOpacity
        },
        Popup = new PopupAdConfig
        {
            Enabled = PopupEnabled, File = PopupFile, StartFrom = PopupStartFrom,
            DurationPerTime = PopupDurationPerTime, TotalPlay = PopupTotalPlay,
            PositionX = PopupPositionX, PositionY = PopupPositionY,
            Width = PopupWidth, Height = PopupHeight,
            CropTop = PopupCropTop, CropRight = PopupCropRight,
            CropBottom = PopupCropBottom, CropLeft = PopupCropLeft,
            Opacity = PopupOpacity
        },
        Tvc = new TvcAdConfig { Enabled = TvcEnabled, File = TvcFile, Count = TvcCount }
    };

    private void UpdateJsonDisplay()
    {
        try { ConfigJsonText = JsonSerializer.Serialize(BuildConfigFromFields(), JsonDisplayOptions); }
        catch { ConfigJsonText = "{ }"; }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        if (string.IsNullOrEmpty(SelectedAdSetName)) { StatusMessage = "No ad set selected"; return; }
        try
        {
            var config = BuildConfigFromFields();
            await _adSetProvider.SaveAdSetConfigAsync(SelectedAdSetName, config);
            UpdateJsonDisplay();
            StatusMessage = $"Saved: {SelectedAdSetName}";
        }
        catch (Exception ex) { Log.Error(ex, "Failed to save"); StatusMessage = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task CaptureFrameAsync()
    {
        try
        {
            var source = _sourcePanelViewModel.CurrentSource;
            if (source is null || !source.IsConnected) { StatusMessage = "No video source connected."; return; }
            LibVLCSharp.Shared.MediaPlayer? mp = source switch
            {
                LocalFileSource l => l.MediaPlayer, YouTubeSource y => y.MediaPlayer,
                LiveFeedSource f => f.MediaPlayer, _ => null
            };
            if (mp is null || !mp.IsPlaying) { StatusMessage = "Video is not playing."; return; }
            StatusMessage = "Capturing frame...";
            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            Directory.CreateDirectory(tempDir);
            var snap = Path.Combine(tempDir, $"ad_frame_{Guid.NewGuid():N}.png");
            mp.TakeSnapshot(0, snap, 0, 0);
            await Task.Delay(800);
            if (File.Exists(snap) && new FileInfo(snap).Length > 0)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(snap); bmp.EndInit(); bmp.Freeze();
                BackgroundFrame = bmp; SaveBackgroundImage(bmp);
                StatusMessage = $"Frame captured ({bmp.PixelWidth}x{bmp.PixelHeight})";
                try { File.Delete(snap); } catch { }
            }
            else StatusMessage = "Failed to capture frame.";
        }
        catch (Exception ex) { Log.Error(ex, "Capture failed"); StatusMessage = $"Error: {ex.Message}"; }
    }

    // ===================== Canvas callbacks =====================

    public void UpdateDoggyPosition(double x, double y, double w, double h)
    {
        DoggyPositionX = Math.Round(x); DoggyPositionY = Math.Round(y);
        DoggyWidth = Math.Round(w); DoggyHeight = Math.Round(h);
    }

    public void UpdateDoggyCrop(double top, double right, double bottom, double left)
    {
        DoggyCropTop = Math.Round(top); DoggyCropRight = Math.Round(right);
        DoggyCropBottom = Math.Round(bottom); DoggyCropLeft = Math.Round(left);
    }

    public void UpdatePopupPosition(double x, double y, double w, double h)
    {
        PopupPositionX = Math.Round(x); PopupPositionY = Math.Round(y);
        PopupWidth = Math.Round(w); PopupHeight = Math.Round(h);
    }

    public void UpdatePopupCrop(double top, double right, double bottom, double left)
    {
        PopupCropTop = Math.Round(top); PopupCropRight = Math.Round(right);
        PopupCropBottom = Math.Round(bottom); PopupCropLeft = Math.Round(left);
    }
}
