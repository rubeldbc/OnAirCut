using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OnAirCut.Recorder.Converters;

/// <summary>
/// Returns Visible when the integer value is greater than 0, Collapsed otherwise.
/// </summary>
public class IntPositiveToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int i && i > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
