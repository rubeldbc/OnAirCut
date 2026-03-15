using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace OnAirCut.Recorder.Converters;

public class BoolToIconConverter : IValueConverter
{
    public PackIconKind TrueIcon { get; set; } = PackIconKind.Pause;
    public PackIconKind FalseIcon { get; set; } = PackIconKind.PlayArrow;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? TrueIcon : FalseIcon;
        return FalseIcon;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
