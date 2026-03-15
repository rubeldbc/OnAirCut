using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OnAirCut.Recorder.ViewModels;

namespace OnAirCut.Recorder.Views;

public partial class SettingsView : UserControl
{
    private bool _isDrawing;
    private Point _startPoint;
    private Rectangle? _selectionRect;

    public SettingsView()
    {
        InitializeComponent();
    }

    private void OcrImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = true;
        _startPoint = e.GetPosition(OcrCanvas);

        _selectionRect = new Rectangle
        {
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255))
        };

        Canvas.SetLeft(_selectionRect, _startPoint.X);
        Canvas.SetTop(_selectionRect, _startPoint.Y);
        _selectionRect.Width = 0;
        _selectionRect.Height = 0;

        OcrCanvas.Children.Clear();
        OcrCanvas.Children.Add(_selectionRect);
        OcrCanvas.CaptureMouse();
    }

    private void OcrImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing || _selectionRect is null) return;

        var pos = e.GetPosition(OcrCanvas);
        var x = Math.Min(pos.X, _startPoint.X);
        var y = Math.Min(pos.Y, _startPoint.Y);
        var w = Math.Abs(pos.X - _startPoint.X);
        var h = Math.Abs(pos.Y - _startPoint.Y);

        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = w;
        _selectionRect.Height = h;
    }

    private void OcrImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing || _selectionRect is null) return;

        _isDrawing = false;
        OcrCanvas.ReleaseMouseCapture();

        if (DataContext is SettingsViewModel vm && OcrImage.Source is BitmapSource bmp)
        {
            // Display coordinates of the drawn rectangle (relative to Canvas)
            var dispX = Canvas.GetLeft(_selectionRect);
            var dispY = Canvas.GetTop(_selectionRect);
            var dispW = _selectionRect.Width;
            var dispH = _selectionRect.Height;

            // Actual image pixel dimensions
            var imgPixelW = (double)bmp.PixelWidth;
            var imgPixelH = (double)bmp.PixelHeight;

            // The Canvas/Image control rendered size
            var ctrlW = OcrCanvas.ActualWidth;
            var ctrlH = OcrCanvas.ActualHeight;

            // With Stretch="Uniform", calculate where the image actually renders
            var scaleToFit = Math.Min(ctrlW / imgPixelW, ctrlH / imgPixelH);
            var renderedW = imgPixelW * scaleToFit;
            var renderedH = imgPixelH * scaleToFit;

            // Offset (letterbox/pillarbox centering)
            var offsetX = (ctrlW - renderedW) / 2.0;
            var offsetY = (ctrlH - renderedH) / 2.0;

            // Convert from display coords to image pixel coords
            var imgX = (dispX - offsetX) / scaleToFit;
            var imgY = (dispY - offsetY) / scaleToFit;
            var imgW = dispW / scaleToFit;
            var imgH = dispH / scaleToFit;

            // Clamp to valid image bounds
            vm.OcrCropX = (int)Math.Max(0, Math.Min(imgX, imgPixelW - 1));
            vm.OcrCropY = (int)Math.Max(0, Math.Min(imgY, imgPixelH - 1));
            vm.OcrCropWidth = (int)Math.Min(imgW, imgPixelW - vm.OcrCropX);
            vm.OcrCropHeight = (int)Math.Min(imgH, imgPixelH - vm.OcrCropY);
        }
    }
}
