using System.Globalization;
using System.Windows.Data;

namespace OnAirCut.RenderServer.Converters;

public class ProgressToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 &&
            values[0] is double progress &&
            values[1] is double maxWidth)
        {
            return Math.Max(0, Math.Min(maxWidth, maxWidth * progress / 100.0));
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
