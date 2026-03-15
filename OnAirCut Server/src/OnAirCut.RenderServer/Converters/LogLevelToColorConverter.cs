using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OnAirCut.RenderServer.Converters;

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string line)
        {
            if (line.Contains("[ERR]") || line.Contains("[FTL]"))
                return new SolidColorBrush(Color.FromRgb(239, 83, 80));    // Red
            if (line.Contains("[WRN]"))
                return new SolidColorBrush(Color.FromRgb(255, 183, 77));   // Yellow/Amber
            if (line.Contains("[INF]"))
                return new SolidColorBrush(Color.FromRgb(224, 224, 224));   // White-ish
            if (line.Contains("[DBG]") || line.Contains("[VRB]"))
                return new SolidColorBrush(Color.FromRgb(117, 117, 117));  // Gray
        }
        return new SolidColorBrush(Color.FromRgb(189, 189, 189)); // Default light gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
