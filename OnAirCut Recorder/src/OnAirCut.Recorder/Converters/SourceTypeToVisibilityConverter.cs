using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OnAirCut.Core.Enums;

namespace OnAirCut.Recorder.Converters;

public class SourceTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SourceType currentType && parameter is string targetTypeStr)
        {
            if (Enum.TryParse<SourceType>(targetTypeStr, out var target))
            {
                return currentType == target ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
