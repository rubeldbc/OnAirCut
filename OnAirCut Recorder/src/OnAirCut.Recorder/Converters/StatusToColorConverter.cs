using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OnAirCut.Core.Enums;

namespace OnAirCut.Recorder.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JobStatus status)
        {
            return status switch
            {
                JobStatus.Pending => new SolidColorBrush(Color.FromRgb(66, 165, 245)),       // Blue
                JobStatus.Processing => new SolidColorBrush(Color.FromRgb(255, 202, 40)),     // Yellow/Amber
                JobStatus.ExtractingFrames => new SolidColorBrush(Color.FromRgb(255, 183, 77)), // Orange
                JobStatus.RunningOcr => new SolidColorBrush(Color.FromRgb(255, 183, 77)),      // Orange
                JobStatus.Rendering => new SolidColorBrush(Color.FromRgb(255, 183, 77)),       // Orange
                JobStatus.Organizing => new SolidColorBrush(Color.FromRgb(129, 199, 132)),     // Light Green
                JobStatus.Completed => new SolidColorBrush(Color.FromRgb(102, 187, 106)),      // Green
                JobStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 83, 80)),           // Red
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
