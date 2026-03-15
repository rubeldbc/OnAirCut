using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using OnAirCut.Core.Interfaces;
using OnAirCut.Recorder.Helpers;
using OnAirCut.Recorder.Services;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class PreviewPlayerViewModel : ObservableObject, IDisposable
{
    private IVideoSource? _source;
    private readonly AudioLevelHelper _audioLevelHelper;
    private Timer? _levelTimer;
    private bool _disposed;

    public PreviewPlayerViewModel()
    {
        _audioLevelHelper = new AudioLevelHelper();
        try
        {
            _audioLevelHelper.Initialize();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Audio level helper initialization failed");
        }
    }

    [ObservableProperty]
    private TimeSpan _currentPosition;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _playbackSpeed = 1.0;

    [ObservableProperty]
    private int _volume = 70;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private double _leftLevel;

    [ObservableProperty]
    private double _rightLevel;

    [ObservableProperty]
    private double _seekPosition;

    [ObservableProperty]
    private bool _hasSource;

    /// <summary>
    /// Raised when a new MediaPlayer is available so the View can attach it to the VideoView.
    /// </summary>
    public event EventHandler<MediaPlayer?>? MediaPlayerChanged;

    public void SetSource(IVideoSource? source)
    {
        if (_source is not null)
        {
            _source.PositionChanged -= OnPositionChanged;
        }

        _source = source;
        HasSource = source is not null;

        if (_source is not null)
        {
            _source.PositionChanged += OnPositionChanged;
            StartLevelMonitoring();

            // Extract the MediaPlayer from the source and notify the View
            var mediaPlayer = GetMediaPlayerFromSource(_source);
            if (mediaPlayer is not null)
            {
                MediaPlayerChanged?.Invoke(this, mediaPlayer);
                IsPlaying = true;
            }
        }
        else
        {
            MediaPlayerChanged?.Invoke(this, null);
            StopLevelMonitoring();
            CurrentPosition = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            IsPlaying = false;
        }
    }

    private static MediaPlayer? GetMediaPlayerFromSource(IVideoSource source)
    {
        // All our source implementations expose MediaPlayer property
        return source switch
        {
            LocalFileSource local => local.MediaPlayer,
            YouTubeSource yt => yt.MediaPlayer,
            LiveFeedSource live => live.MediaPlayer,
            _ => null
        };
    }

    private void OnPositionChanged(object? sender, SourcePositionChangedEventArgs e)
    {
        CurrentPosition = e.Position;
        if (e.Duration.HasValue)
        {
            Duration = e.Duration.Value;
            if (Duration.TotalMilliseconds > 0)
            {
                SeekPosition = e.Position.TotalMilliseconds / Duration.TotalMilliseconds * 100;
            }
        }
    }

    private void StartLevelMonitoring()
    {
        _levelTimer?.Dispose();
        _levelTimer = new Timer(_ =>
        {
            _audioLevelHelper.UpdateLevels();
            LeftLevel = _audioLevelHelper.LeftLevel;
            RightLevel = _audioLevelHelper.RightLevel;
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
    }

    private void StopLevelMonitoring()
    {
        _levelTimer?.Dispose();
        _levelTimer = null;
        LeftLevel = 0;
        RightLevel = 0;
    }

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        if (_source is null) return;

        try
        {
            if (IsPlaying)
            {
                await _source.StopPreviewAsync();
                IsPlaying = false;
            }
            else
            {
                await _source.StartPreviewAsync();
                IsPlaying = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PlayPause failed");
        }
    }

    [RelayCommand]
    private void Seek(double position)
    {
        if (_source is null || Duration.TotalMilliseconds <= 0) return;
        SeekPosition = position;
    }

    [RelayCommand]
    private void ChangeSpeed(string speedStr)
    {
        if (double.TryParse(speedStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            PlaybackSpeed = speed;
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    partial void OnVolumeChanged(int value)
    {
        var mp = _source is not null ? GetMediaPlayerFromSource(_source) : null;
        if (mp is not null)
        {
            mp.Volume = IsMuted ? 0 : value;
        }
    }

    partial void OnIsMutedChanged(bool value)
    {
        var mp = _source is not null ? GetMediaPlayerFromSource(_source) : null;
        if (mp is not null)
        {
            mp.Volume = value ? 0 : Volume;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _levelTimer?.Dispose();
        _audioLevelHelper.Dispose();
    }
}
