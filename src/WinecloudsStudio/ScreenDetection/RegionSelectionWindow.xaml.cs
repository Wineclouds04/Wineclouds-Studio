using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using WinecloudsStudio.Detection;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace WinecloudsStudio.ScreenDetection;

public sealed partial class RegionSelectionWindow : Window
{
    private readonly ScreenRegion _virtualScreen = VirtualScreenService.GetBounds();
    private TaskCompletionSource<ScreenRegion?>? _completion;
    private PointI _start;
    private bool _dragging;
    private bool _completed;

    public RegionSelectionWindow()
    {
        InitializeComponent();
        Closed += RegionSelectionWindow_Closed;
        Activated += RegionSelectionWindow_Activated;
    }

    public async Task<ScreenRegion?> ShowAsync()
    {
        if (_completion is not null)
            throw new InvalidOperationException("区域选择窗口不能重复显示。");

        _completion = new TaskCompletionSource<ScreenRegion?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        DesktopImage.Source = CaptureVirtualDesktop();
        ConfigureWindow();
        Activate();
        return await _completion.Task;
    }

    private void ConfigureWindow()
    {
        IntPtr handle = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        appWindow.MoveAndResize(new RectInt32(
            _virtualScreen.X,
            _virtualScreen.Y,
            _virtualScreen.Width,
            _virtualScreen.Height));
    }

    private void RegionSelectionWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        SelectionSurface.Focus(FocusState.Programmatic);
    }

    private void SelectionSurface_PointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (!GetCursorPos(out _start))
            return;

        _dragging = true;
        SelectionSurface.CapturePointer(args.Pointer);
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(_start);
        args.Handled = true;
    }

    private void SelectionSurface_PointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging && GetCursorPos(out PointI current))
            UpdateSelection(current);
    }

    private void SelectionSurface_PointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!_dragging)
            return;

        _dragging = false;
        SelectionSurface.ReleasePointerCapture(args.Pointer);
        if (!GetCursorPos(out PointI current))
            return;

        ScreenRegion region = Normalize(_start, current);
        if (region.IsValid)
            Complete(region);
        else
            SelectionBorder.Visibility = Visibility.Collapsed;

        args.Handled = true;
    }

    private void SelectionSurface_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Escape)
            return;

        args.Handled = true;
        Complete(null);
    }

    private void UpdateSelection(PointI current)
    {
        ScreenRegion region = Normalize(_start, current);
        double scale = SelectionSurface.XamlRoot?.RasterizationScale ?? 1d;
        Canvas.SetLeft(SelectionBorder, (region.X - _virtualScreen.X) / scale);
        Canvas.SetTop(SelectionBorder, (region.Y - _virtualScreen.Y) / scale);
        SelectionBorder.Width = Math.Max(1, region.Width / scale);
        SelectionBorder.Height = Math.Max(1, region.Height / scale);
        SelectionSizeText.Text = $"X {region.X}, Y {region.Y}  |  {region.Width} × {region.Height}";
    }

    private WriteableBitmap CaptureVirtualDesktop()
    {
        using ScreenCaptureService capture = new();
        using CapturedFrame frame = capture.Capture(_virtualScreen);

        int rowLength = checked(frame.Width * 4);
        byte[] pixels = new byte[checked(rowLength * frame.Height)];
        for (int y = 0; y < frame.Height; y++)
        {
            frame.Pixels.Slice(y * frame.Stride, rowLength)
                .CopyTo(pixels.AsSpan(y * rowLength, rowLength));
        }

        WriteableBitmap bitmap = new(frame.Width, frame.Height);
        using Stream stream = bitmap.PixelBuffer.AsStream();
        stream.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    private static ScreenRegion Normalize(PointI first, PointI second)
    {
        int left = Math.Min(first.X, second.X);
        int top = Math.Min(first.Y, second.Y);
        return new ScreenRegion(
            left,
            top,
            Math.Abs(first.X - second.X),
            Math.Abs(first.Y - second.Y));
    }

    private void Complete(ScreenRegion? region)
    {
        if (_completed)
            return;

        _completed = true;
        _completion?.TrySetResult(region);
        Close();
    }

    private void RegionSelectionWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_completed)
            return;

        _completed = true;
        _completion?.TrySetResult(null);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointI
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out PointI point);
}
