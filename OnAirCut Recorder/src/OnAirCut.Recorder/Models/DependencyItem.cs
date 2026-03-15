using CommunityToolkit.Mvvm.ComponentModel;

namespace OnAirCut.Recorder.Models;

public partial class DependencyItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private string _status = "Checking...";

    [ObservableProperty]
    private string _size = "";

    [ObservableProperty]
    private double _downloadProgress; // 0-100

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _downloadSpeed = "";

    [ObservableProperty]
    private string _eta = "";

    public string CheckPath { get; set; } = ""; // Path to check if installed
    public string SecondaryCheckPath { get; set; } = ""; // Optional second path to check
    public string DownloadUrl { get; set; } = ""; // URL to download from
    public string DestinationFolder { get; set; } = ""; // Where to extract/save
    public bool IsZip { get; set; } // Whether the download is a zip that needs extraction
    public bool IsPythonSetup { get; set; } // Special flag for Python+EasyOCR complex install
}
