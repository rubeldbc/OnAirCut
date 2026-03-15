using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OnAirCut.Core.Enums;

namespace OnAirCut.RenderServer.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JobStatus status)
        {
            return status switch
            {
                JobStatus.Pending => new SolidColorBrush(Color.FromRgb(66, 165, 245)),       // Blue
                JobStatus.Processing => new SolidColorBrush(Color.FromRgb(255, 183, 77)),     // Amber
                JobStatus.ExtractingFrames => new SolidColorBrush(Color.FromRgb(255, 183, 77)),
                JobStatus.RunningOcr => new SolidColorBrush(Color.FromRgb(206, 147, 216)),    // Purple
                JobStatus.Rendering => new SolidColorBrush(Color.FromRgb(255, 167, 38)),      // Orange
                JobStatus.Organizing => new SolidColorBrush(Color.FromRgb(129, 199, 132)),    // Light Green
                JobStatus.Completed => new SolidColorBrush(Color.FromRgb(102, 187, 106)),     // Green
                JobStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 83, 80)),          // Red
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))                        // Gray
            };
        }

        if (value is string statusStr && Enum.TryParse<JobStatus>(statusStr, out var parsed))
        {
            return Convert(parsed, targetType, parameter, culture);
        }

        return new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
