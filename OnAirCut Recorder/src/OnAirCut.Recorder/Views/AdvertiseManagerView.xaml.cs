using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OnAirCut.Recorder.ViewModels;

namespace OnAirCut.Recorder.Views;

public partial class AdvertiseManagerView : UserControl
{
    // Each overlay has: border, media, 4 corner handles (resize), 4 edge handles (crop)
    private OverlayInfo? _doggy;
    private OverlayInfo? _popup;

    private enum DragMode { None, Move, ResizeTL, ResizeTR, ResizeBL, ResizeBR, CropTop, CropRight, CropBottom, CropLeft }
    private DragMode _dragMode = DragMode.None;
    private OverlayInfo? _dragTarget;
    private Point _dragStart;
    private double _startLeft, _startTop, _startW, _startH;
    private double _startCropT, _startCropR, _startCropB, _startCropL;
    private const double HS = 8; // handle size

    private double _scale = 1, _offsetX, _offsetY;
    private const double OW = 1920, OH = 1080;

    private class OverlayInfo
    {
        public Border Border = null!;
        public MediaElement Media = null!;
        public Rectangle[] CornerHandles = new Rectangle[4]; // TL TR BL BR
        public Rectangle[] EdgeHandles = new Rectangle[4];   // Top Right Bottom Left
        public bool IsPopup;
    }

    public AdvertiseManagerView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is AdvertiseManagerViewModel o) o.PropertyChanged -= OnVmProp;
            if (e.NewValue is AdvertiseManagerViewModel n) n.PropertyChanged += OnVmProp;
        };
        Loaded += (_, _) =>
        {
            if (VM is not null && VM.AdPanelWidth > 0) PanelColumn.Width = new GridLength(VM.AdPanelWidth);
            UpdateAllOverlays();
        };
    }

    private AdvertiseManagerViewModel? VM => DataContext as AdvertiseManagerViewModel;

    private void OnVmProp(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_dragMode != DragMode.None) return;
        var n = e.PropertyName;
        if (n is null) return;
        if (n.StartsWith("Doggy") || n.StartsWith("Popup") || n == nameof(AdvertiseManagerViewModel.BackgroundFrame))
            Dispatcher.InvokeAsync(UpdateAllOverlays);
        if (n == nameof(AdvertiseManagerViewModel.DoggyFileFullPath))
            Dispatcher.InvokeAsync(() => PlayVideo(_doggy, VM?.DoggyFileFullPath));
        if (n == nameof(AdvertiseManagerViewModel.PopupFileFullPath))
            Dispatcher.InvokeAsync(() => PlayVideo(_popup, VM?.PopupFileFullPath));
    }

    private void AdCanvas_SizeChanged(object s, SizeChangedEventArgs e) { Recalc(); UpdateAllOverlays(); }

    private void Recalc()
    {
        var cw = AdCanvas.ActualWidth; var ch = AdCanvas.ActualHeight;
        if (cw <= 0 || ch <= 0) return;
        _scale = Math.Min(cw / OW, ch / OH);
        _offsetX = (cw - OW * _scale) / 2;
        _offsetY = (ch - OH * _scale) / 2;
    }

    // =================== Build overlay elements ===================

    private OverlayInfo EnsureOverlay(ref OverlayInfo? info, Brush color, bool isPopup)
    {
        if (info is not null) return info;
        var media = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Fill, IsMuted = true };
        media.MediaEnded += (s, _) => { if (s is MediaElement m) { m.Position = TimeSpan.Zero; m.Play(); } };
        var border = new Border
        {
            BorderBrush = color, BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
            CornerRadius = new CornerRadius(2), Child = media, Cursor = Cursors.SizeAll
        };
        AdCanvas.Children.Add(border);
        var ov = new OverlayInfo { Border = border, Media = media, IsPopup = isPopup };
        // Corner handles (resize) - white
        for (int i = 0; i < 4; i++)
        {
            var h = new Rectangle { Width = HS, Height = HS, Fill = Brushes.White, Stroke = color, StrokeThickness = 1.5 };
            h.Cursor = (i is 0 or 3) ? Cursors.SizeNWSE : Cursors.SizeNESW;
            ov.CornerHandles[i] = h;
            AdCanvas.Children.Add(h);
        }
        // Edge handles (crop) - yellow diamonds
        var cropBrush = Brushes.Yellow;
        for (int i = 0; i < 4; i++)
        {
            var h = new Rectangle { Width = HS, Height = HS, Fill = cropBrush, Stroke = Brushes.DarkGoldenrod, StrokeThickness = 1 };
            h.RenderTransform = new RotateTransform(45, HS / 2, HS / 2);
            h.Cursor = (i is 0 or 2) ? Cursors.SizeNS : Cursors.SizeWE;
            ov.EdgeHandles[i] = h;
            AdCanvas.Children.Add(h);
        }
        info = ov;
        return ov;
    }

    // =================== Update overlays from VM ===================

    private void UpdateAllOverlays()
    {
        if (VM is null) return;
        Recalc();
        UpdateOverlay(ref _doggy, Brushes.Lime, false,
            VM.DoggyEnabled, VM.DoggyPositionX, VM.DoggyPositionY, VM.DoggyWidth, VM.DoggyHeight,
            VM.DoggyCropTop, VM.DoggyCropRight, VM.DoggyCropBottom, VM.DoggyCropLeft);
        UpdateOverlay(ref _popup, Brushes.Cyan, true,
            VM.PopupEnabled, VM.PopupPositionX, VM.PopupPositionY, VM.PopupWidth, VM.PopupHeight,
            VM.PopupCropTop, VM.PopupCropRight, VM.PopupCropBottom, VM.PopupCropLeft);
    }

    private void UpdateOverlay(ref OverlayInfo? info, Brush color, bool isPopup,
        bool enabled, double px, double py, double pw, double ph,
        double ct, double cr, double cb, double cl)
    {
        if (!enabled || VM?.BackgroundFrame is null)
        {
            SetVis(info, Visibility.Collapsed);
            return;
        }
        var ov = EnsureOverlay(ref info, color, isPopup);
        var w = Math.Max(20, pw * _scale);
        var h = Math.Max(20, ph * _scale);
        var left = px * _scale + _offsetX;
        var top = py * _scale + _offsetY;
        ov.Border.Width = w; ov.Border.Height = h;
        Canvas.SetLeft(ov.Border, left); Canvas.SetTop(ov.Border, top);
        // Apply crop as clip on the media
        var clipL = cl * _scale / (pw > 0 ? 1 : 1);
        // Simplified: crop is stored in logical px, clip is relative to the border
        ov.Media.Clip = new RectangleGeometry(new Rect(
            cl * _scale * w / (pw * _scale > 0 ? pw * _scale : 1),
            ct * _scale * h / (ph * _scale > 0 ? ph * _scale : 1),
            w - (cl + cr) * _scale * w / (pw * _scale > 0 ? pw * _scale : 1),
            h - (ct + cb) * _scale * h / (ph * _scale > 0 ? ph * _scale : 1)));
        SetVis(ov, Visibility.Visible);
        PosCorners(ov, left, top, w, h);
        PosEdges(ov, left, top, w, h, ct * _scale, cr * _scale, cb * _scale, cl * _scale);
    }

    private void PosCorners(OverlayInfo ov, double l, double t, double w, double h)
    {
        var d = HS / 2;
        Canvas.SetLeft(ov.CornerHandles[0], l - d); Canvas.SetTop(ov.CornerHandles[0], t - d);
        Canvas.SetLeft(ov.CornerHandles[1], l + w - d); Canvas.SetTop(ov.CornerHandles[1], t - d);
        Canvas.SetLeft(ov.CornerHandles[2], l - d); Canvas.SetTop(ov.CornerHandles[2], t + h - d);
        Canvas.SetLeft(ov.CornerHandles[3], l + w - d); Canvas.SetTop(ov.CornerHandles[3], t + h - d);
    }

    private void PosEdges(OverlayInfo ov, double l, double t, double w, double h,
        double cropT, double cropR, double cropB, double cropL)
    {
        var d = HS / 2;
        // Top edge: center-top + crop offset
        Canvas.SetLeft(ov.EdgeHandles[0], l + w / 2 - d); Canvas.SetTop(ov.EdgeHandles[0], t + cropT - d);
        // Right edge: right - crop offset
        Canvas.SetLeft(ov.EdgeHandles[1], l + w - cropR - d); Canvas.SetTop(ov.EdgeHandles[1], t + h / 2 - d);
        // Bottom edge: center-bottom - crop offset
        Canvas.SetLeft(ov.EdgeHandles[2], l + w / 2 - d); Canvas.SetTop(ov.EdgeHandles[2], t + h - cropB - d);
        // Left edge: left + crop offset
        Canvas.SetLeft(ov.EdgeHandles[3], l + cropL - d); Canvas.SetTop(ov.EdgeHandles[3], t + h / 2 - d);
    }

    private void SetVis(OverlayInfo? ov, Visibility v)
    {
        if (ov is null) return;
        ov.Border.Visibility = v;
        foreach (var h in ov.CornerHandles) if (h is not null) h.Visibility = v;
        foreach (var h in ov.EdgeHandles) if (h is not null) h.Visibility = v;
        if (v == Visibility.Collapsed) StopVideo(ov);
    }

    // =================== Video playback ===================

    private void PlayVideo(OverlayInfo? ov, string? path)
    {
        if (ov is null) return;
        if (string.IsNullOrEmpty(path)) { StopVideo(ov); return; }
        try { ov.Media.Source = new Uri(path); ov.Media.Play(); } catch { }
    }

    private void StopVideo(OverlayInfo? ov)
    {
        if (ov is null) return;
        try { ov.Media.Stop(); ov.Media.Source = null; } catch { }
    }

    // =================== Hit testing ===================

    private (DragMode mode, OverlayInfo? target) HitTest(Point p)
    {
        // Check both overlays, popup on top
        var result = HitTestOverlay(_popup, p);
        if (result.mode != DragMode.None) return result;
        return HitTestOverlay(_doggy, p);
    }

    private (DragMode mode, OverlayInfo? target) HitTestOverlay(OverlayInfo? ov, Point p)
    {
        if (ov is null || ov.Border.Visibility != Visibility.Visible) return (DragMode.None, null);
        var ex = HS + 2;
        // Check edge handles first (crop)
        for (int i = 0; i < 4; i++)
        {
            var eh = ov.EdgeHandles[i];
            var el = Canvas.GetLeft(eh); var et = Canvas.GetTop(eh);
            if (p.X >= el - 2 && p.X <= el + HS + 2 && p.Y >= et - 2 && p.Y <= et + HS + 2)
                return (i switch { 0 => DragMode.CropTop, 1 => DragMode.CropRight, 2 => DragMode.CropBottom, _ => DragMode.CropLeft }, ov);
        }
        // Check corner handles (resize)
        for (int i = 0; i < 4; i++)
        {
            var ch = ov.CornerHandles[i];
            var cl = Canvas.GetLeft(ch); var ct = Canvas.GetTop(ch);
            if (p.X >= cl - 2 && p.X <= cl + HS + 2 && p.Y >= ct - 2 && p.Y <= ct + HS + 2)
                return (i switch { 0 => DragMode.ResizeTL, 1 => DragMode.ResizeTR, 2 => DragMode.ResizeBL, _ => DragMode.ResizeBR }, ov);
        }
        // Check inside rect (move)
        var bl = Canvas.GetLeft(ov.Border); var bt = Canvas.GetTop(ov.Border);
        if (p.X >= bl && p.X <= bl + ov.Border.Width && p.Y >= bt && p.Y <= bt + ov.Border.Height)
            return (DragMode.Move, ov);
        return (DragMode.None, null);
    }

    // =================== Mouse events ===================

    private void AdCanvas_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
    {
        if (VM is null) return;
        var p = e.GetPosition(AdCanvas);
        var (mode, target) = HitTest(p);
        if (mode == DragMode.None || target is null) return;
        _dragMode = mode; _dragTarget = target; _dragStart = p;
        _startLeft = Canvas.GetLeft(target.Border); _startTop = Canvas.GetTop(target.Border);
        _startW = target.Border.Width; _startH = target.Border.Height;
        // Store current crop in canvas pixels
        if (target == _doggy)
        {
            _startCropT = VM.DoggyCropTop * _scale; _startCropR = VM.DoggyCropRight * _scale;
            _startCropB = VM.DoggyCropBottom * _scale; _startCropL = VM.DoggyCropLeft * _scale;
        }
        else
        {
            _startCropT = VM.PopupCropTop * _scale; _startCropR = VM.PopupCropRight * _scale;
            _startCropB = VM.PopupCropBottom * _scale; _startCropL = VM.PopupCropLeft * _scale;
        }
        AdCanvas.CaptureMouse(); e.Handled = true;
    }

    private void AdCanvas_MouseMove(object s, MouseEventArgs e)
    {
        if (VM is null) return;
        var p = e.GetPosition(AdCanvas);

        if (_dragMode != DragMode.None && _dragTarget is not null)
        {
            var dx = p.X - _dragStart.X; var dy = p.Y - _dragStart.Y;
            double nl = _startLeft, nt = _startTop, nw = _startW, nh = _startH;
            double ncT = _startCropT, ncR = _startCropR, ncB = _startCropB, ncL = _startCropL;

            switch (_dragMode)
            {
                case DragMode.Move:
                    nl = Math.Max(_offsetX, Math.Min(_startLeft + dx, _offsetX + OW * _scale - nw));
                    nt = Math.Max(_offsetY, Math.Min(_startTop + dy, _offsetY + OH * _scale - nh));
                    break;
                case DragMode.ResizeBR: nw = Math.Max(30, _startW + dx); nh = Math.Max(30, _startH + dy); break;
                case DragMode.ResizeTL:
                    nw = Math.Max(30, _startW - dx); nh = Math.Max(30, _startH - dy);
                    nl = _startLeft + _startW - nw; nt = _startTop + _startH - nh; break;
                case DragMode.ResizeTR: nw = Math.Max(30, _startW + dx); nh = Math.Max(30, _startH - dy); nt = _startTop + _startH - nh; break;
                case DragMode.ResizeBL: nw = Math.Max(30, _startW - dx); nh = Math.Max(30, _startH + dy); nl = _startLeft + _startW - nw; break;
                case DragMode.CropTop: ncT = Math.Max(0, Math.Min(_startCropT + dy, nh - 30)); break;
                case DragMode.CropBottom: ncB = Math.Max(0, Math.Min(_startCropB - dy, nh - 30)); break;
                case DragMode.CropLeft: ncL = Math.Max(0, Math.Min(_startCropL + dx, nw - 30)); break;
                case DragMode.CropRight: ncR = Math.Max(0, Math.Min(_startCropR - dx, nw - 30)); break;
            }

            _dragTarget.Border.Width = nw; _dragTarget.Border.Height = nh;
            Canvas.SetLeft(_dragTarget.Border, nl); Canvas.SetTop(_dragTarget.Border, nt);
            PosCorners(_dragTarget, nl, nt, nw, nh);
            PosEdges(_dragTarget, nl, nt, nw, nh, ncT, ncR, ncB, ncL);

            // Update VM
            var lx = (nl - _offsetX) / _scale; var ly = (nt - _offsetY) / _scale;
            var lw = nw / _scale; var lh = nh / _scale;
            if (_dragTarget == _doggy)
            {
                VM.UpdateDoggyPosition(lx, ly, lw, lh);
                VM.UpdateDoggyCrop(ncT / _scale, ncR / _scale, ncB / _scale, ncL / _scale);
            }
            else
            {
                VM.UpdatePopupPosition(lx, ly, lw, lh);
                VM.UpdatePopupCrop(ncT / _scale, ncR / _scale, ncB / _scale, ncL / _scale);
            }
        }
        else
        {
            var (mode, _) = HitTest(p);
            AdCanvas.Cursor = mode switch
            {
                DragMode.Move => Cursors.SizeAll,
                DragMode.ResizeTL or DragMode.ResizeBR => Cursors.SizeNWSE,
                DragMode.ResizeTR or DragMode.ResizeBL => Cursors.SizeNESW,
                DragMode.CropTop or DragMode.CropBottom => Cursors.SizeNS,
                DragMode.CropLeft or DragMode.CropRight => Cursors.SizeWE,
                _ => Cursors.Arrow
            };
        }
    }

    private void AdCanvas_MouseLeftButtonUp(object s, MouseButtonEventArgs e)
    {
        if (_dragMode != DragMode.None)
        {
            _dragMode = DragMode.None; _dragTarget = null;
            AdCanvas.ReleaseMouseCapture(); e.Handled = true;
        }
    }

    // =================== Panel width persistence ===================

    private void GridSplitter_DragCompleted(object s, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (VM is not null) VM.AdPanelWidth = PanelColumn.Width.Value;
    }

    // =================== Accordion ===================

    private bool _suppressAccordion;
    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        if (_suppressAccordion) return;
        _suppressAccordion = true;
        if (((Expander)sender).Parent is Panel parent)
            foreach (var c in parent.Children.OfType<Expander>())
                if (c != sender && c.IsExpanded) c.IsExpanded = false;
        _suppressAccordion = false;
    }
}
