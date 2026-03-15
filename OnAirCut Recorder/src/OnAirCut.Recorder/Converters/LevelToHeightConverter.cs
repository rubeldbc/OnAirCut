using System.Globalization;
using System.Windows.Data;

namespace OnAirCut.Recorder.Converters;

public class LevelToHeightConverter : IValueConverter
{
    public static readonly LevelToHeightConverter Instance = new();

    private const double MaxHeight = 80.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double level)
        {
            return Math.Clamp(level, 0, 1) * MaxHeight;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
