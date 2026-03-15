using System.Windows.Controls;
using System.Windows.Input;
using OnAirCut.Recorder.ViewModels;

namespace OnAirCut.Recorder.Views;

public partial class RecordingControlsView : UserControl
{
    public RecordingControlsView()
    {
        InitializeComponent();
    }

    private void CapturedText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not RecordingControlsViewModel vm) return;
        if (string.IsNullOrEmpty(vm.LastCapturedText)) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Ctrl+Click: open the saved text file location
            var lastSavedPath = vm.LastSavedFilePath;
            if (!string.IsNullOrEmpty(lastSavedPath) && System.IO.File.Exists(lastSavedPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{lastSavedPath}\"");
            }
        }
        else
        {
            // Click: copy text to clipboard
            System.Windows.Clipboard.SetText(vm.LastCapturedText);
            vm.StatusMessage = "Text copied to clipboard";
        }
    }
}
