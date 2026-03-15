using System.Windows;
using System.Windows.Controls;
using OnAirCut.Recorder.ViewModels;

namespace OnAirCut.Recorder.Views;

public partial class PreviewPlayerView : UserControl
{
    public PreviewPlayerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is PreviewPlayerViewModel vm)
        {
            vm.MediaPlayerChanged += OnMediaPlayerChanged;
        }
        if (e.OldValue is PreviewPlayerViewModel oldVm)
        {
            oldVm.MediaPlayerChanged -= OnMediaPlayerChanged;
        }
    }

    private void OnMediaPlayerChanged(object? sender, LibVLCSharp.Shared.MediaPlayer? mediaPlayer)
    {
        Dispatcher.Invoke(() =>
        {
            VlcVideoView.MediaPlayer = mediaPlayer;
        });
    }
}
