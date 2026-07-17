using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using WinecloudsStudio.Modules.WindowManager.Services.Interface;
using WinecloudsStudio.Modules.WindowManager.Services.Interop;
using WinRT.Interop;

namespace WinecloudsStudio.Modules.WindowManager.Views;

public sealed partial class ThumbnailPopupWindow : Window, IThumbnailView
{
    private const int BorderWidth = 3;
    private const int GridSize = 8;

    private readonly IWindowManager _windowManager;
    private IDwmThumbnail? _thumbnail;
    private bool _showBorder;
    private bool _highlighted;
    private bool _positionLocked;
    private bool _snapToGrid;
    private bool _isExcludedFromCycleGroup;
    private bool _isDragging;
    private bool _activated;
    private IntPtr _hwnd;
    private int _startCursorX;
    private int _startCursorY;
    private int _windowStartX;
    private int _windowStartY;
    private int _viewWidth;
    private int _viewHeight;
    private double _opacity = 1.0;
    private string _title;
    private string _processName;

    // Cached DWM thumbnail destination rect (inset when border is visible).
    private int _destLeft;
    private int _destTop;
    private int _destRight;
    private int _destBottom;

    public ThumbnailPopupWindow(
        IWindowManager windowManager,
        IntPtr id,
        string processName,
        string title,
        int width,
        int height,
        int x,
        int y)
    {
        _windowManager = windowManager;
        _processName = processName;
        _title = title;
        _viewWidth = width;
        _viewHeight = height;
        Id = id;

        this.InitializeComponent();

        TitleLabel.Text = title;

        RootGrid.PointerPressed += OnPointerPressed;
        RootGrid.PointerMoved += OnPointerMoved;
        RootGrid.PointerReleased += OnPointerReleased;

        this.Closed += OnClosed;
        this.Activated += OnFirstActivated;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        AppWindow.IsShownInSwitchers = false;

        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    // ----------------------------------------------------------------------
    //  IThumbnailView
    // ----------------------------------------------------------------------

    public IntPtr Id { get; }
    public IntPtr Handle => _hwnd;
    public string ProcessName => _processName;

    public new string Title
    {
        get => _title;
        set
        {
            _title = value;
            base.Title = value;
            if (DispatcherQueue.HasThreadAccess)
                UpdateTitleLabel();
            else
                _ = DispatcherQueue.TryEnqueue(UpdateTitleLabel);
        }
    }

    public Point ThumbnailLocation
    {
        get
        {
            PointInt32 pos = AppWindow.Position;
            return new Point(pos.X, pos.Y);
        }
        set => AppWindow.Move(new PointInt32(value.X, value.Y));
    }

    public Size ThumbnailSize
    {
        get => new Size(_viewWidth, _viewHeight);
        set
        {
            _viewWidth = value.Width;
            _viewHeight = value.Height;
            AppWindow.Resize(new SizeInt32(value.Width, value.Height));
        }
    }

    public bool IsActive => _activated;

    public bool IsExcludedFromCycleGroup
    {
        get => _isExcludedFromCycleGroup;
        set
        {
            if (_isExcludedFromCycleGroup == value) return;
            _isExcludedFromCycleGroup = value;
            UpdateTitleLabel();
        }
    }

    public event Action<IntPtr>? ThumbnailActivated;
    public event Action<IntPtr>? ThumbnailMoved;
    public event Action<IntPtr>? ThumbnailCycleGroupToggled;

    public bool IsKnownHandle(IntPtr handle) =>
        handle == Id || handle == _hwnd;

    public void ShowThumbnail()
    {
        Activate();
    }

    public void RefreshThumbnail(bool forceRefresh)
    {
        if (_hwnd == IntPtr.Zero) return;

        RefreshLiveThumbnail(forceRefresh);
        ResizeLiveThumbnail();
    }

    public void SetOpacity(double opacity)
    {
        _opacity = Math.Clamp(opacity, 0.1, 1.0);
        if (_opacity >= 0.9) _opacity = 1.0;

        if (_hwnd != IntPtr.Zero)
            ApplyWindowOpacity();
    }

    public void SetTopMost(bool topMost)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = topMost;
    }

    public void SetPositionLocked(bool locked) =>
        _positionLocked = locked;

    public void SetSnapToGrid(bool snapToGrid) =>
        _snapToGrid = snapToGrid;

    public void SetHighlight(bool highlighted)
    {
        if (_highlighted == highlighted) return;
        _highlighted = highlighted;
        ApplyBorder();
    }

    public void SetShowBorder(bool showBorder)
    {
        if (_showBorder == showBorder) return;
        _showBorder = showBorder;
        ApplyBorder();
    }

    public void SetOverlayLabel(bool showLabel) =>
        TitleBorder.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;

    // ----------------------------------------------------------------------
    //  Activation / window setup
    // ----------------------------------------------------------------------

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_activated) return;
        _activated = true;

        _hwnd = WindowNative.GetWindowHandle(this);

        nint exStyle = GetWindowLongPtr(_hwnd, InteropConstants.GWL_EXSTYLE);
        exStyle |= (nint)InteropConstants.WS_EX_TOOLWINDOW;
        _ = SetWindowLongPtr(_hwnd, InteropConstants.GWL_EXSTYLE, exStyle);

        ApplyWindowOpacity();

        RefreshLiveThumbnail(true);
        ResizeLiveThumbnail();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _thumbnail?.Unregister();
        _thumbnail = null;
        _activated = false;
    }

    public void Dispose()
    {
        if (_activated)
            Close();
    }

    // ----------------------------------------------------------------------
    //  Pointer handlers — left-click activate, right-click drag to move
    // ----------------------------------------------------------------------

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var props = args.GetCurrentPoint(RootGrid).Properties;

        if (props.IsLeftButtonPressed)
        {
            if (args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
                ThumbnailCycleGroupToggled?.Invoke(Id);
            else
                ThumbnailActivated?.Invoke(Id);
            args.Handled = true;
            return;
        }

        if (props.IsRightButtonPressed && !_positionLocked)
        {
            RootGrid.CapturePointer(args.Pointer);
            GetCursorPos(out var cursorPos);
            _startCursorX = cursorPos.X;
            _startCursorY = cursorPos.Y;
            PointInt32 windowPos = AppWindow.Position;
            _windowStartX = windowPos.X;
            _windowStartY = windowPos.Y;
            _isDragging = true;
            args.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!_isDragging) return;

        if (!args.GetCurrentPoint(RootGrid).Properties.IsRightButtonPressed)
        {
            StopDrag(args.Pointer);
            return;
        }

        GetCursorPos(out var cursorPos);
        int newX = _windowStartX + (cursorPos.X - _startCursorX);
        int newY = _windowStartY + (cursorPos.Y - _startCursorY);

        AppWindow.Move(new PointInt32(newX, newY));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!_isDragging) return;
        StopDrag(args.Pointer);
        ThumbnailMoved?.Invoke(Id);
    }

    private void StopDrag(Pointer pointer)
    {
        _isDragging = false;
        RootGrid.ReleasePointerCapture(pointer);
        if (_snapToGrid)
        {
            PointInt32 position = AppWindow.Position;
            AppWindow.Move(new PointInt32(
                (int)Math.Round(position.X / (double)GridSize) * GridSize,
                (int)Math.Round(position.Y / (double)GridSize) * GridSize));
        }
    }

    // ----------------------------------------------------------------------
    //  DWM thumbnail management
    // ----------------------------------------------------------------------

    private void RefreshLiveThumbnail(bool forceRefresh)
    {
        if (_hwnd == IntPtr.Zero) return;

        if (_thumbnail == null || forceRefresh)
        {
            _thumbnail?.Unregister();
            _thumbnail = _windowManager.GetLiveThumbnail(_hwnd, Id);
        }

        _thumbnail?.Update();
    }

    private void ResizeLiveThumbnail()
    {
        int inset = (_showBorder || _highlighted) ? BorderWidth : 0;
        int left = inset;
        int top = inset;
        int right = Math.Max(1, _viewWidth - inset);
        int bottom = Math.Max(1, _viewHeight - inset);

        if (left == _destLeft && top == _destTop && right == _destRight && bottom == _destBottom)
            return;

        _destLeft = left;
        _destTop = top;
        _destRight = right;
        _destBottom = bottom;
        _thumbnail?.Move(left, top, right, bottom);
        _thumbnail?.Update();
    }

    // ----------------------------------------------------------------------
    //  Border / highlight — uses Border overlay, NOT Grid background.
    //  XAML sits above DWM thumbnail; a solid Grid background would occlude it.
    // ----------------------------------------------------------------------

    private void ApplyBorder()
    {
        bool active = _showBorder || _highlighted;
        HighlightBorder.BorderThickness = active
            ? new Thickness(BorderWidth)
            : new Thickness(0);
        ResizeLiveThumbnail();
    }

    private void UpdateTitleLabel()
    {
        TitleLabel.Text = _isExcludedFromCycleGroup ? $"[已排除] {_title}" : _title;
        TitleLabel.Foreground = _isExcludedFromCycleGroup
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.OrangeRed)
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
    }

    // ----------------------------------------------------------------------
    //  Opacity via SetLayeredWindowAttributes (LWA_ALPHA confirmed to
    //  work on WinUI 3, unlike LWA_COLORKEY).
    // ----------------------------------------------------------------------

    private void ApplyWindowOpacity()
    {
        if (_hwnd == IntPtr.Zero) return;

        if (_opacity < 1.0)
        {
            nint exStyle = GetWindowLongPtr(_hwnd, InteropConstants.GWL_EXSTYLE);
            if ((exStyle & (nint)InteropConstants.WS_EX_LAYERED) == 0)
            {
                exStyle |= (nint)InteropConstants.WS_EX_LAYERED;
                _ = SetWindowLongPtr(_hwnd, InteropConstants.GWL_EXSTYLE, exStyle);
            }

            _ = SetLayeredWindowAttributes(_hwnd, 0, (byte)(_opacity * 255), LWA_ALPHA);
        }
        else
        {
            nint exStyle = GetWindowLongPtr(_hwnd, InteropConstants.GWL_EXSTYLE);
            if ((exStyle & (nint)InteropConstants.WS_EX_LAYERED) != 0)
            {
                exStyle &= ~(nint)InteropConstants.WS_EX_LAYERED;
                _ = SetWindowLongPtr(_hwnd, InteropConstants.GWL_EXSTYLE, exStyle);
            }
        }
    }

    // ----------------------------------------------------------------------
    //  P/Invoke
    // ----------------------------------------------------------------------

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(
        nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const uint LWA_ALPHA = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
