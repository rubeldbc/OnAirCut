using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OnAirCut.Recorder.Models;

namespace OnAirCut.Recorder.Converters;

public class RecordingStateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RecordingState state && parameter is string targetState)
        {
            var states = targetState.Split(',');
            foreach (var s in states)
            {
                if (Enum.TryParse<RecordingState>(s.Trim(), out var target) && state == target)
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
