using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class OcrRegionViewModel : ObservableObject
{
    private readonly IOcrProfileProvider? _ocrProfileProvider;

    public OcrRegionViewModel(IOcrProfileProvider? ocrProfileProvider = null)
    {
        _ocrProfileProvider = ocrProfileProvider;
    }

    [ObservableProperty]
    private BitmapImage? _frameImage;

    [ObservableProperty]
    private int _cropX;

    [ObservableProperty]
    private int _cropY;

    [ObservableProperty]
    private int _cropWidth;

    [ObservableProperty]
    private int _cropHeight;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private string _sourceName = string.Empty;

    [ObservableProperty]
    private double _resizeScale = 2.0;

    [ObservableProperty]
    private ThresholdMode _thresholdMode = ThresholdMode.None;

    [ObservableProperty]
    private ObservableCollection<OcrProfile> _availableProfiles = [];

    [ObservableProperty]
    private OcrProfile? _selectedProfile;

    [ObservableProperty]
    private string _testResult = string.Empty;

    public Array ThresholdModes => Enum.GetValues<ThresholdMode>();

    partial void OnSelectedProfileChanged(OcrProfile? value)
    {
        if (value is null) return;
        ProfileName = value.ProfileName;
        SourceName = value.SourceName;
        CropX = value.CropX;
        CropY = value.CropY;
        CropWidth = value.CropWidth;
        CropHeight = value.CropHeight;
        ResizeScale = value.ResizeScale;
        ThresholdMode = value.ThresholdMode;
    }

    [RelayCommand]
    private void CaptureFrame()
    {
        // Would capture current video frame; placeholder for now
        TestResult = "Frame capture not implemented in this context";
    }

    [RelayCommand]
    private void LoadImage()
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
            }
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (_ocrProfileProvider is null || string.IsNullOrWhiteSpace(ProfileName)) return;

        try
        {
            var profile = new OcrProfile
            {
                ProfileName = ProfileName,
                SourceName = SourceName,
                CropX = CropX,
                CropY = CropY,
                CropWidth = CropWidth,
                CropHeight = CropHeight,
                ResizeScale = ResizeScale,
                ThresholdMode = ThresholdMode,
                IsActive = true
            };

            await _ocrProfileProvider.SaveProfileAsync(profile);
            TestResult = "Profile saved";
            await LoadProfilesAsync();
        }
        catch (Exception ex)
        {
            TestResult = $"Save failed: {ex.Message}";
            Log.Error(ex, "Failed to save OCR profile");
        }
    }

    [RelayCommand]
    private void TestOcr()
    {
        TestResult = "OCR test requires render server integration";
    }

    [RelayCommand]
    private async Task DeleteProfileAsync()
    {
        if (_ocrProfileProvider is null || string.IsNullOrWhiteSpace(ProfileName)) return;

        try
        {
            await _ocrProfileProvider.DeleteProfileAsync(ProfileName);
            TestResult = "Profile deleted";
            ProfileName = string.Empty;
            await LoadProfilesAsync();
        }
        catch (Exception ex)
        {
            TestResult = $"Delete failed: {ex.Message}";
            Log.Error(ex, "Failed to delete OCR profile");
        }
    }

    public async Task LoadProfilesAsync()
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
}
