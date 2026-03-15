using System.Globalization;
using System.Windows.Data;
using OnAirCut.Core.Enums;

namespace OnAirCut.Recorder.Converters;

public class SourceTypeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SourceType currentType && parameter is string targetTypeStr)
        {
            if (Enum.TryParse<SourceType>(targetTypeStr, out var target))
            {
                return currentType == target;
            }
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string targetTypeStr)
        {
            if (Enum.TryParse<SourceType>(targetTypeStr, out var target))
            {
                return target;
            }
        }
        return Binding.DoNothing;
    }
}
