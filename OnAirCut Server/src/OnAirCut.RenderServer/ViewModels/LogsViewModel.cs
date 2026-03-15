using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.RenderServer.Services;
using Serilog;

namespace OnAirCut.RenderServer.ViewModels;

public partial class LogsViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _tailTimer;
    private long _lastReadPosition;
    private string _currentLogFile = string.Empty;

    [ObservableProperty]
    private string _filterLevel = "All";

    [ObservableProperty]
    private string _filterJobId = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isAutoScroll = true;

    public ObservableCollection<string> LogEntries { get; } = [];

    public List<string> LevelOptions { get; } = ["All", "DBG", "INF", "WRN", "ERR", "FTL"];

    public event EventHandler? ScrollRequested;

    public LogsViewModel()
    {
        _tailTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tailTimer.Tick += (_, _) => TailLogFile();
        _tailTimer.Start();
    }

    private void TailLogFile()
    {
        try
        {
            var logFile = LoggingService.GetCurrentLogFilePath();

            if (logFile != _currentLogFile)
            {
                _currentLogFile = logFile;
                _lastReadPosition = 0;
            }

            if (!File.Exists(logFile)) return;

            using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length <= _lastReadPosition) return;

            fs.Seek(_lastReadPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null) break;

                if (ShouldShowLine(line))
                {
                    LogEntries.Add(line);
                }
            }

            _lastReadPosition = fs.Position;

            // Keep max 5000 entries
            while (LogEntries.Count > 5000)
                LogEntries.RemoveAt(0);

            if (IsAutoScroll)
                ScrollRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Silently ignore log reading errors
            System.Diagnostics.Debug.WriteLine($"Log tail error: {ex.Message}");
        }
    }

    private bool ShouldShowLine(string line)
    {
        // Filter by level
        if (FilterLevel != "All" && !line.Contains($"[{FilterLevel}]"))
            return false;

        // Filter by JobId
        if (!string.IsNullOrWhiteSpace(FilterJobId) &&
            !line.Contains(FilterJobId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !line.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    [RelayCommand]
    private void ClearDisplay()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private void OpenLogFile()
    {
        var logFile = LoggingService.GetCurrentLogFilePath();
        if (File.Exists(logFile))
        {
            Process.Start(new ProcessStartInfo { FileName = logFile, UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void Filter()
    {
        // Re-read from scratch with new filters
        LogEntries.Clear();
        _lastReadPosition = 0;
        TailLogFile();
    }

    public void Dispose()
    {
        _tailTimer.Stop();
    }
}
