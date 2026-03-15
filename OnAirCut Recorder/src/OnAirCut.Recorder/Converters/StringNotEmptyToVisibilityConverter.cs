using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OnAirCut.Recorder.Converters;

/// <summary>
/// Returns Visible when the string value is not null or empty, Collapsed otherwise.
/// </summary>
public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string s && !string.IsNullOrEmpty(s)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
