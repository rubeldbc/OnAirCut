using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Recorder.Services;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class SourcePanelViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly Func<SourceType, IVideoSource> _sourceFactory;
    private IVideoSource? _currentSource;

    public SourcePanelViewModel(ISettingsService settingsService, Func<SourceType, IVideoSource> sourceFactory)
    {
        _settingsService = settingsService;
        _sourceFactory = sourceFactory;

        // Load last used source from settings
        var settings = settingsService.Settings;
        _selectedSourceType = settings.DefaultSourceType;
        _sourceUri = settings.LastSourceUri;
        _selectedDevice = settings.LastSourceDevice;

        // Load OCR profiles from shared folder
        _ = LoadOcrProfilesAsync();

        // Re-load profiles when settings change (e.g. shared folder path changes)
        _settingsService.SettingsChanged += (_, _) => _ = LoadOcrProfilesAsync();
    }

    [ObservableProperty]
    private SourceType _selectedSourceType;

    [ObservableProperty]
    private string _sourceUri = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private ObservableCollection<string> _availableDevices = new() { "Default Capture Device" };

    [ObservableProperty]
    private string? _selectedDevice;

    [ObservableProperty]
    private ObservableCollection<string> _ocrProfileNames = new();

    [ObservableProperty]
    private string? _selectedOcrProfile;

    public IVideoSource? CurrentSource => _currentSource;

    public event EventHandler<IVideoSource?>? SourceChanged;

    partial void OnSelectedSourceTypeChanged(SourceType value)
    {
        if (IsConnected)
        {
            _ = DisconnectAsync();
        }

        // Restore the last used URI for this source type
        var settings = _settingsService.Settings;
        if (value == settings.DefaultSourceType)
        {
            SourceUri = settings.LastSourceUri;
            SelectedDevice = settings.LastSourceDevice;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            ConnectionStatus = "Connecting...";

            _currentSource?.Dispose();
            _currentSource = _sourceFactory(SelectedSourceType);

            var uri = SelectedSourceType switch
            {
                SourceType.LiveFeed => SelectedDevice ?? "Default Capture Device",
                SourceType.LocalFile => SourceUri,
                SourceType.YouTubeUrl => SourceUri,
                _ => SourceUri
            };

            if (string.IsNullOrWhiteSpace(uri))
            {
                ConnectionStatus = "Please specify a source";
                return;
            }

            var success = await _currentSource.ConnectAsync(uri);
            IsConnected = success;
            ConnectionStatus = success ? "Connected" : "Connection failed";

            if (success)
            {
                // Save last used source to settings
                _settingsService.UpdateSettings(s =>
                {
                    s.DefaultSourceType = SelectedSourceType;
                    s.LastSourceUri = SourceUri;
                    s.LastSourceDevice = SelectedDevice ?? string.Empty;
                });
                await _settingsService.SaveAsync();
                Log.Information("Saved last source: {Type} = {Uri}", SelectedSourceType, uri);

                SourceChanged?.Invoke(this, _currentSource);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect source");
            ConnectionStatus = $"Error: {ex.Message}";
            IsConnected = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            if (_currentSource is not null)
            {
                await _currentSource.DisconnectAsync();
                _currentSource.Dispose();
                _currentSource = null;
            }
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            SourceChanged?.Invoke(this, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to disconnect source");
        }
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Video File",
            Filter = "Video Files|*.mp4;*.mkv;*.mov;*.avi;*.ts;*.flv;*.wmv;*.webm|All Files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            SourceUri = dialog.FileName;
        }
    }

    partial void OnSelectedOcrProfileChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        _ = ApplyOcrProfileAsync(value);
    }

    private async Task ApplyOcrProfileAsync(string profileName)
    {
        try
        {
            var sharedFolder = _settingsService.Settings.SharedFolderPath;
            if (string.IsNullOrWhiteSpace(sharedFolder)) return;

            var filePath = Path.Combine(sharedFolder, "Assets", "OcrProfiles", $"{profileName}.json");
            if (!File.Exists(filePath)) return;

            var json = await File.ReadAllTextAsync(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _settingsService.UpdateSettings(s =>
            {
                if (root.TryGetProperty("cropX", out var cropX)) s.OcrCropX = cropX.GetInt32();
                if (root.TryGetProperty("cropY", out var cropY)) s.OcrCropY = cropY.GetInt32();
                if (root.TryGetProperty("cropWidth", out var cropW)) s.OcrCropWidth = cropW.GetInt32();
                if (root.TryGetProperty("cropHeight", out var cropH)) s.OcrCropHeight = cropH.GetInt32();
            });
            await _settingsService.SaveAsync();

            Log.Information("OCR profile applied from source panel: {Name}", profileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply OCR profile: {Name}", profileName);
        }
    }

    public async Task LoadOcrProfilesAsync()
    {
        try
        {
            var sharedFolder = _settingsService.Settings.SharedFolderPath;
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
            Log.Error(ex, "Failed to load OCR profiles in source panel");
        }
    }
}
