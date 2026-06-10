using System.Globalization;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PhotoQuick.Domain;
using PhotoQuick.ViewModels;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfCursors = System.Windows.Input.Cursors;

namespace PhotoQuick;

public partial class MainWindow : Window
{
    private const double MinScale = 1;
    private const double MaxScale = 12;
    private const double ZoomStep = 1.12;
    private const double AnimationSmoothing = 0.14;
    private const double AnimationEpsilon = 0.001;
    private const double MinImageSurfaceWidth = 360;
    private const double MinSidePanelWidth = 320;
    private const double MaxSidePanelWidth = 620;
    private const double DefaultSidePanelWidth = 348;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    private WpfPoint _dragStart;
    private bool _isDragging;
    private bool _isAnimatingTransform;
    private bool _isAnimatingFileListScroll;
    private ScrollViewer? _fileListScrollViewer;
    private double _lastSidePanelWidth = DefaultSidePanelWidth;
    private double _targetScale = 1;
    private double _targetTranslateX;
    private double _targetTranslateY;
    private double _targetFileListOffset;
    private int _rotationDegrees;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        DataContext = viewModel;
        SourceInitialized += Window_OnSourceInitialized;
        Focusable = true;
        Focus();
    }

    private void ImageSurface_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        var nextScale = Math.Clamp(_targetScale * factor, MinScale, MaxScale);
        var mouse = e.GetPosition(ImageSurface);
        var anchor = GetNearestImagePointUnderMouse(mouse);

        _targetTranslateX = mouse.X - anchor.X * nextScale;
        _targetTranslateY = mouse.Y - anchor.Y * nextScale;
        _targetScale = nextScale;
        ClampTargetImageTransform();

        StartTransformAnimation();
        e.Handled = true;
    }

    private void ImageSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        ImageSurface.CaptureMouse();
        Cursor = WpfCursors.Hand;
    }

    private void ImageSurface_OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var point = e.GetPosition(this);
        var deltaX = point.X - _dragStart.X;
        var deltaY = point.Y - _dragStart.Y;

        _targetTranslateX += deltaX;
        _targetTranslateY += deltaY;
        ImageTranslate.X += deltaX;
        ImageTranslate.Y += deltaY;
        ClampTargetImageTransform();
        ClampCurrentImageTransform();
        _dragStart = point;
    }

    private void ImageSurface_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ImageSurface.ReleaseMouseCapture();
        Cursor = WpfCursors.Arrow;
    }

    private void ImageSurface_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ResetImageTransform();
        }
    }

    private void ImageSurface_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_targetScale >= MinScale && ImageScale.ScaleX >= MinScale)
        {
            return;
        }

        _targetScale = MinScale;
        ImageScale.ScaleX = MinScale;
        ImageScale.ScaleY = MinScale;
        ClampTargetImageTransform();
        ClampCurrentImageTransform();
        StartTransformAnimation();
    }

    private void Window_OnKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Left && vm.PreviousCommand.CanExecute(null))
        {
            vm.PreviousCommand.Execute(null);
            ResetImageTransform();
        }
        else if (e.Key == Key.Right && vm.NextCommand.CanExecute(null))
        {
            vm.NextCommand.Execute(null);
            ResetImageTransform();
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse state changes during the native move loop.
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_OnStateChanged(object? sender, EventArgs e)
    {
        MaximizeRestoreIcon.Data = Geometry.Parse(WindowState == WindowState.Maximized
            ? "M 6 3 L 13 3 L 13 10 M 3 6 L 10 6 L 10 13 L 3 13 Z"
            : "M 3 3 L 13 3 L 13 13 L 3 13 Z");

        if (WindowState != WindowState.Maximized)
        {
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
        }
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private static bool IsInsideButton(DependencyObject source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is System.Windows.Controls.Button)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void ResetImageTransform()
    {
        _targetScale = 1;
        _targetTranslateX = 0;
        _targetTranslateY = 0;
        ImageScale.ScaleX = 1;
        ImageScale.ScaleY = 1;
        ImageTranslate.X = 0;
        ImageTranslate.Y = 0;
    }

    private void RotateRightButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DisplayedImage.Source is null)
        {
            return;
        }

        _rotationDegrees = (_rotationDegrees + 90) % 360;
        ImageRotate.Angle = _rotationDegrees;
        ResetImageTransform();
    }

    private void SidePanelSplitter_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _lastSidePanelWidth = Math.Clamp(SidePanelColumn.ActualWidth, MinSidePanelWidth, MaxSidePanelWidth);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ShowSidePanel) || sender is not MainViewModel vm)
        {
            return;
        }

        SetSidePanelVisible(vm.ShowSidePanel);
    }

    private void SetSidePanelVisible(bool isVisible)
    {
        if (isVisible)
        {
            SidePanelColumn.MinWidth = MinSidePanelWidth;
            SidePanelColumn.MaxWidth = MaxSidePanelWidth;
            var maxWidth = Math.Min(MaxSidePanelWidth, Math.Max(MinSidePanelWidth, ActualWidth - MinImageSurfaceWidth));
            var width = Math.Clamp(_lastSidePanelWidth <= 0 ? DefaultSidePanelWidth : _lastSidePanelWidth, MinSidePanelWidth, maxWidth);
            SideSplitterColumn.Width = new GridLength(4);
            SidePanelColumn.Width = new GridLength(width);
            return;
        }

        if (SidePanelColumn.ActualWidth > 0)
        {
            _lastSidePanelWidth = SidePanelColumn.ActualWidth;
        }

        SideSplitterColumn.Width = new GridLength(0);
        SidePanelColumn.MinWidth = 0;
        SidePanelColumn.MaxWidth = 0;
        SidePanelColumn.Width = new GridLength(0);
    }

    private void FileListBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        _fileListScrollViewer = FindVisualChild<ScrollViewer>(FileListBox);
        _targetFileListOffset = _fileListScrollViewer?.VerticalOffset ?? 0;
    }

    private void FileListBox_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_fileListScrollViewer is null)
        {
            return;
        }

        var delta = -e.Delta * 0.55;
        _targetFileListOffset = Math.Clamp(
            _targetFileListOffset + delta,
            0,
            _fileListScrollViewer.ScrollableHeight);

        StartFileListScrollAnimation();
        e.Handled = true;
    }

    private void StartTransformAnimation()
    {
        if (_isAnimatingTransform)
        {
            return;
        }

        _isAnimatingTransform = true;
        CompositionTarget.Rendering += AnimateTransform;
    }

    private void StartFileListScrollAnimation()
    {
        if (_isAnimatingFileListScroll)
        {
            return;
        }

        _isAnimatingFileListScroll = true;
        CompositionTarget.Rendering += AnimateFileListScroll;
    }

    private void AnimateTransform(object? sender, EventArgs e)
    {
        ClampTargetImageTransform();
        ImageScale.ScaleX = Lerp(ImageScale.ScaleX, _targetScale, AnimationSmoothing);
        ImageScale.ScaleY = ImageScale.ScaleX;
        ImageTranslate.X = Lerp(ImageTranslate.X, _targetTranslateX, AnimationSmoothing);
        ImageTranslate.Y = Lerp(ImageTranslate.Y, _targetTranslateY, AnimationSmoothing);
        ClampCurrentImageTransform();

        if (Math.Abs(ImageScale.ScaleX - _targetScale) > AnimationEpsilon ||
            Math.Abs(ImageTranslate.X - _targetTranslateX) > AnimationEpsilon ||
            Math.Abs(ImageTranslate.Y - _targetTranslateY) > AnimationEpsilon)
        {
            return;
        }

        ImageScale.ScaleX = _targetScale;
        ImageScale.ScaleY = _targetScale;
        ImageTranslate.X = _targetTranslateX;
        ImageTranslate.Y = _targetTranslateY;
        CompositionTarget.Rendering -= AnimateTransform;
        _isAnimatingTransform = false;
    }

    private void AnimateFileListScroll(object? sender, EventArgs e)
    {
        if (_fileListScrollViewer is null)
        {
            CompositionTarget.Rendering -= AnimateFileListScroll;
            _isAnimatingFileListScroll = false;
            return;
        }

        var current = _fileListScrollViewer.VerticalOffset;
        var next = Lerp(current, _targetFileListOffset, 0.22);
        _fileListScrollViewer.ScrollToVerticalOffset(next);

        if (Math.Abs(next - _targetFileListOffset) > 0.5)
        {
            return;
        }

        _fileListScrollViewer.ScrollToVerticalOffset(_targetFileListOffset);
        CompositionTarget.Rendering -= AnimateFileListScroll;
        _isAnimatingFileListScroll = false;
    }

    private static double Lerp(double current, double target, double amount) =>
        current + (target - current) * amount;

    private void ClampTargetImageTransform()
    {
        (_targetTranslateX, _targetTranslateY) = ClampImageTranslation(
            _targetScale,
            _targetTranslateX,
            _targetTranslateY);
    }

    private void ClampCurrentImageTransform()
    {
        (ImageTranslate.X, ImageTranslate.Y) = ClampImageTranslation(
            ImageScale.ScaleX,
            ImageTranslate.X,
            ImageTranslate.Y);
    }

    private (double X, double Y) ClampImageTranslation(double scale, double x, double y)
    {
        var rect = GetFittedImageRect();
        if (rect is null)
        {
            return (0, 0);
        }

        var surfaceWidth = ImageSurface.ActualWidth;
        var surfaceHeight = ImageSurface.ActualHeight;

        return (
            ClampAxis(x, rect.Value.Left, rect.Value.Width, surfaceWidth, scale),
            ClampAxis(y, rect.Value.Top, rect.Value.Height, surfaceHeight, scale));
    }

    private WpfPoint GetNearestImagePointUnderMouse(WpfPoint mouse)
    {
        var rect = GetFittedImageRect();
        if (rect is null)
        {
            return new WpfPoint(0, 0);
        }

        var unscaledX = (mouse.X - _targetTranslateX) / _targetScale;
        var unscaledY = (mouse.Y - _targetTranslateY) / _targetScale;

        return new WpfPoint(
            Math.Clamp(unscaledX, rect.Value.Left, rect.Value.Right),
            Math.Clamp(unscaledY, rect.Value.Top, rect.Value.Bottom));
    }

    private System.Windows.Rect? GetFittedImageRect()
    {
        if (DisplayedImage.Source is null || ImageSurface.ActualWidth <= 0 || ImageSurface.ActualHeight <= 0)
        {
            return null;
        }

        var surfaceWidth = ImageSurface.ActualWidth;
        var surfaceHeight = ImageSurface.ActualHeight;
        var sourceWidth = DisplayedImage.Source.Width;
        var sourceHeight = DisplayedImage.Source.Height;

        if (_rotationDegrees is 90 or 270)
        {
            (sourceWidth, sourceHeight) = (sourceHeight, sourceWidth);
        }

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return null;
        }

        var fitScale = Math.Min(surfaceWidth / sourceWidth, surfaceHeight / sourceHeight);
        var fittedWidth = sourceWidth * fitScale;
        var fittedHeight = sourceHeight * fitScale;

        return new System.Windows.Rect(
            (surfaceWidth - fittedWidth) / 2,
            (surfaceHeight - fittedHeight) / 2,
            fittedWidth,
            fittedHeight);
    }

    private static double ClampAxis(double translate, double fittedStart, double fittedLength, double viewportLength, double scale)
    {
        var scaledStart = fittedStart * scale;
        var scaledLength = fittedLength * scale;

        if (scaledLength <= viewportLength)
        {
            return (viewportLength - scaledLength) / 2 - scaledStart;
        }

        var min = viewportLength - (scaledStart + scaledLength);
        var max = -scaledStart;
        return Math.Clamp(translate, min, max);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }
    }

    private nint WindowProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo)
        {
            return nint.Zero;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == nint.Zero)
        {
            return nint.Zero;
        }

        var monitorInfo = new MonitorInfo();
        monitorInfo.Size = Marshal.SizeOf<MonitorInfo>();
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return nint.Zero;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.Monitor;

        minMaxInfo.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        minMaxInfo.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        minMaxInfo.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        minMaxInfo.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
        handled = true;
        return nint.Zero;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public int Flags;
    }
}

public sealed class SortModeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            SortMode.FileNameAsc => "文件名 A-Z",
            SortMode.FileNameDesc => "文件名 Z-A",
            SortMode.LastWriteTimeAsc => "修改时间 旧-新",
            SortMode.LastWriteTimeDesc => "修改时间 新-旧",
            _ => value?.ToString() ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}

public sealed class ObjectToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}

public sealed class ObjectToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}

public sealed class BooleanToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var expandedWidth = 328d;
        if (parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            expandedWidth = parsed;
        }

        return value is true
            ? new GridLength(expandedWidth)
            : new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
