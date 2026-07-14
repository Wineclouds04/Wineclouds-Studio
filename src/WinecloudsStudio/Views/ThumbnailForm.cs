using System.Runtime.InteropServices;
using WinecloudsStudio.Services.Interface;
using WinecloudsStudio.Services.Interop;

namespace WinecloudsStudio.Views;

// RedrawWindow flags
file static class RDW
{
    public const uint ERASE = 0x0004;
    public const uint INVALIDATE = 0x0001;
    public const uint FRAME = 0x0400;
    public const uint ALLCHILDREN = 0x0080;
}

/// <summary>
/// Lightweight native Win32 popup window that hosts a DWM live thumbnail.
/// Left-click = activate target. Right-click drag = move.
/// </summary>
public sealed class ThumbnailWindow : IDisposable
{
    private const string WindowClassName = "WinecloudsThumbnailWnd";
    private const int BORDER_WIDTH = 3;
    private const int TITLE_BAR_HEIGHT = 24;
    // COLORREF format: 0x00BBGGRR — high byte must be 0x00
    private static readonly uint NEON_GREEN = 0x0000FF00; // B=0x00 G=0xFF R=0x00
    private static readonly uint COLOR_BLACK = 0x00000000;
    private static readonly uint TITLE_BAR_BG = 0x002D2D2D; // dark gray
    private static readonly uint TITLE_TEXT_COLOR = 0x00FFFFFF; // white

    private static bool s_classRegistered;
    private static readonly object s_classLock = new();
    private static readonly Dictionary<IntPtr, WeakReference<ThumbnailWindow>> s_refMap = new();

    private readonly IWindowManager _windowManager;
    private IDwmThumbnail? _dwmThumbnail;

    private IntPtr _hwnd;
    private IntPtr _targetHwnd;
    private string _title;
    private readonly string _processName;
    private int _width;
    private int _height;
    private bool _isDisposed;

    // Initial position (restored from saved config)
    private readonly int? _initialX;
    private readonly int? _initialY;

    // Right-click drag
    private bool _isDragging;
    private int _dragStartX, _dragStartY;
    private int _windowStartX, _windowStartY;

    // Border
    private bool _showBorder;

    public event Action<IntPtr>? OnThumbnailActivated;

    public ThumbnailWindow(IntPtr targetHandle, string processName, string windowTitle,
        IWindowManager windowManager, int? initialX = null, int? initialY = null)
    {
        _windowManager = windowManager;
        _targetHwnd = targetHandle;
        _title = windowTitle;
        _processName = processName;
        _width = 280;
        _height = 180;
        _initialX = initialX;
        _initialY = initialY;

        EnsureClassRegistered();
        _hwnd = CreateNativeWindow();

        lock (s_refMap) { s_refMap[_hwnd] = new WeakReference<ThumbnailWindow>(this); }
        User32NativeMethods.ShowWindowAsync(_hwnd, InteropConstants.SW_SHOWNOACTIVATE);
        RegisterDwmThumbnail();
    }

    public IntPtr Handle => _hwnd;
    public IntPtr TargetHandle => _targetHwnd;
    public string Title => _title;
    public string ProcessName => _processName;

    public void UpdateTitle(string newTitle) { _title = newTitle; }

    public void SetThumbnailSize(int width, int height)
    {
        if (_width == width && _height == height) return;
        _width = width;
        _height = height;
        User32NativeMethods.GetWindowRect(_hwnd, out RECT r);
        User32NativeMethods.MoveWindow(_hwnd, r.Left, r.Top, width, height + TITLE_BAR_HEIGHT, true);
        MoveDwmThumbnail();
    }

    public void SetOpacity(double opacity) { MoveDwmThumbnail(); }

    public void SetTopMost(bool enable)
    {
        User32NativeMethods.SetWindowPos(_hwnd,
            enable ? User32NativeMethods.HWND_TOPMOST : IntPtr.Zero,
            0, 0, 0, 0,
            User32NativeMethods.SWP_NOMOVE | User32NativeMethods.SWP_NOSIZE | User32NativeMethods.SWP_SHOWWINDOW);
    }

    public void SetShowBorder(bool show)
    {
        if (_showBorder == show) return;
        _showBorder = show;
        MoveDwmThumbnail();
        // Force full repaint including background erase
        RedrawWindow(_hwnd, IntPtr.Zero, IntPtr.Zero,
            RDW.ERASE | RDW.INVALIDATE | RDW.FRAME | RDW.ALLCHILDREN);
    }

    public void SetHighlight(bool highlighted) { }

    /// <summary>
    /// Returns the current screen position (top-left corner) of this window.
    /// </summary>
    public (int X, int Y) GetPosition()
    {
        User32NativeMethods.GetWindowRect(_hwnd, out RECT r);
        return (r.Left, r.Top);
    }

    public void Show()
    {
        User32NativeMethods.ShowWindowAsync(_hwnd, InteropConstants.SW_SHOWNOACTIVATE);
        RefreshThumbnail();
    }

    public void CloseThumbnail()
    {
        try { _dwmThumbnail?.Unregister(); _dwmThumbnail = null; } catch { }
        if (_hwnd != IntPtr.Zero)
        {
            lock (s_refMap) { s_refMap.Remove(_hwnd); }
            User32NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    public void RefreshThumbnail()
    {
        if (_dwmThumbnail == null) RegisterDwmThumbnail();
        MoveDwmThumbnail();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        CloseThumbnail();
        OnThumbnailActivated = null;
    }

    // ---- Private ----

    private static void EnsureClassRegistered()
    {
        lock (s_classLock)
        {
            if (s_classRegistered) return;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = User32NativeMethods.CS_HREDRAW | User32NativeMethods.CS_VREDRAW,
                lpfnWndProc = StaticWndProc,
                hInstance = User32NativeMethods.GetModuleHandle(null),
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszClassName = WindowClassName
            };
            User32NativeMethods.RegisterClassEx(ref wc);
            s_classRegistered = true;
        }
    }

    private IntPtr CreateNativeWindow()
    {
        uint exStyle = InteropConstants.WS_EX_TOOLWINDOW | InteropConstants.WS_EX_TOPMOST;
        uint style = InteropConstants.WS_POPUP
                     | InteropConstants.WS_CLIPCHILDREN | InteropConstants.WS_CLIPSIBLINGS
                     | InteropConstants.WS_VISIBLE;

        int x, y;
        if (_initialX.HasValue && _initialY.HasValue)
        {
            x = _initialX.Value;
            y = _initialY.Value;
        }
        else
        {
            int offset = s_refMap.Count * 30;
            x = 100 + offset;
            y = 100 + offset;
        }

        int windowHeight = _height + TITLE_BAR_HEIGHT;
        var hwnd = User32NativeMethods.CreateWindowEx(
            exStyle, WindowClassName, _title, style,
            x, y, _width, windowHeight,
            IntPtr.Zero, IntPtr.Zero,
            User32NativeMethods.GetModuleHandle(null), IntPtr.Zero);

        User32NativeMethods.ShowWindowAsync(hwnd, InteropConstants.SW_SHOWNOACTIVATE);
        return hwnd;
    }

    private void RegisterDwmThumbnail()
    {
        try { _dwmThumbnail = _windowManager.GetLiveThumbnail(_hwnd, _targetHwnd); }
        catch { _dwmThumbnail = null; }
    }

    private void MoveDwmThumbnail()
    {
        if (_dwmThumbnail == null) return;

        int windowHeight = _height + TITLE_BAR_HEIGHT;

        if (_showBorder)
        {
            // Inset the thumbnail to leave a border gap (title bar also gets border)
            _dwmThumbnail.Move(
                BORDER_WIDTH,
                TITLE_BAR_HEIGHT + BORDER_WIDTH,
                _width - BORDER_WIDTH,
                windowHeight - BORDER_WIDTH);
        }
        else
        {
            _dwmThumbnail.Move(0, TITLE_BAR_HEIGHT, _width, windowHeight);
        }

        _dwmThumbnail.Update();
    }

    // ---- Drag ----
    private void BeginDrag()
    {
        User32NativeMethods.GetCursorPos(out POINT c);
        _dragStartX = c.X; _dragStartY = c.Y;
        User32NativeMethods.GetWindowRect(_hwnd, out RECT r);
        _windowStartX = r.Left; _windowStartY = r.Top;
        _isDragging = true;
    }
    private void DoDrag()
    {
        if (!_isDragging) return;
        User32NativeMethods.GetCursorPos(out POINT c);
        User32NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero,
            _windowStartX + c.X - _dragStartX, _windowStartY + c.Y - _dragStartY,
            _width, _height + TITLE_BAR_HEIGHT,
            User32NativeMethods.SWP_NOZORDER | User32NativeMethods.SWP_SHOWWINDOW);
    }
    private void EndDrag() { _isDragging = false; }

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    // ---- WndProc ----
    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        ThumbnailWindow? inst = null;
        lock (s_refMap)
        {
            if (s_refMap.TryGetValue(hWnd, out var wr))
                wr.TryGetTarget(out inst);
        }

        switch (msg)
        {
            case User32NativeMethods.WM_ERASEBKGND:
                // Paint: title bar (dark gray) + content area (black), or green border around both
                if (inst != null)
                {
                    IntPtr hdc = wParam;
                    User32NativeMethods.GetClientRect(hWnd, out RECT clientRect);

                    if (inst._showBorder)
                    {
                        // Green border fills entire window
                        var greenBrush = User32NativeMethods.CreateSolidBrush(NEON_GREEN);
                        User32NativeMethods.FillRect(hdc, ref clientRect, greenBrush);
                        User32NativeMethods.DeleteObject(greenBrush);

                        // Title bar background (inset by border)
                        var titleBrush = User32NativeMethods.CreateSolidBrush(TITLE_BAR_BG);
                        RECT titleRect = new RECT(
                            BORDER_WIDTH, BORDER_WIDTH,
                            clientRect.Right - BORDER_WIDTH, BORDER_WIDTH + TITLE_BAR_HEIGHT);
                        User32NativeMethods.FillRect(hdc, ref titleRect, titleBrush);
                        User32NativeMethods.DeleteObject(titleBrush);

                        // Content area background (black, below title bar, inset by border)
                        var blackBrush = User32NativeMethods.CreateSolidBrush(COLOR_BLACK);
                        RECT contentRect = new RECT(
                            BORDER_WIDTH, BORDER_WIDTH + TITLE_BAR_HEIGHT,
                            clientRect.Right - BORDER_WIDTH, clientRect.Bottom - BORDER_WIDTH);
                        User32NativeMethods.FillRect(hdc, ref contentRect, blackBrush);
                        User32NativeMethods.DeleteObject(blackBrush);

                        // Draw window title text
                        User32NativeMethods.SetBkMode(hdc, User32NativeMethods.TRANSPARENT);
                        User32NativeMethods.SetTextColor(hdc, TITLE_TEXT_COLOR);
                        RECT textRect = new RECT(
                            BORDER_WIDTH + 8, BORDER_WIDTH,
                            clientRect.Right - BORDER_WIDTH - 4, BORDER_WIDTH + TITLE_BAR_HEIGHT);
                        User32NativeMethods.DrawTextW(hdc, inst._title, -1, ref textRect,
                            User32NativeMethods.DT_LEFT | User32NativeMethods.DT_VCENTER
                            | User32NativeMethods.DT_SINGLELINE | User32NativeMethods.DT_NOPREFIX);
                    }
                    else
                    {
                        // Title bar background
                        var titleBrush = User32NativeMethods.CreateSolidBrush(TITLE_BAR_BG);
                        RECT titleRect = new RECT(0, 0, clientRect.Right, TITLE_BAR_HEIGHT);
                        User32NativeMethods.FillRect(hdc, ref titleRect, titleBrush);
                        User32NativeMethods.DeleteObject(titleBrush);

                        // Content area background (black, below title bar)
                        var blackBrush = User32NativeMethods.CreateSolidBrush(COLOR_BLACK);
                        RECT contentRect = new RECT(0, TITLE_BAR_HEIGHT, clientRect.Right, clientRect.Bottom);
                        User32NativeMethods.FillRect(hdc, ref contentRect, blackBrush);
                        User32NativeMethods.DeleteObject(blackBrush);

                        // Draw window title text
                        User32NativeMethods.SetBkMode(hdc, User32NativeMethods.TRANSPARENT);
                        User32NativeMethods.SetTextColor(hdc, TITLE_TEXT_COLOR);
                        RECT textRect = new RECT(8, 0, clientRect.Right - 4, TITLE_BAR_HEIGHT);
                        User32NativeMethods.DrawTextW(hdc, inst._title, -1, ref textRect,
                            User32NativeMethods.DT_LEFT | User32NativeMethods.DT_VCENTER
                            | User32NativeMethods.DT_SINGLELINE | User32NativeMethods.DT_NOPREFIX);
                    }
                }
                return (IntPtr)1; // handled

            case User32NativeMethods.WM_LBUTTONDOWN:
                inst?.OnThumbnailActivated?.Invoke(inst._targetHwnd);
                break;

            case User32NativeMethods.WM_RBUTTONDOWN:
                inst?.BeginDrag();
                break;

            case User32NativeMethods.WM_MOUSEMOVE:
                inst?.DoDrag();
                break;

            case User32NativeMethods.WM_RBUTTONUP:
                inst?.EndDrag();
                break;

            case User32NativeMethods.WM_DESTROY:
                lock (s_refMap) { s_refMap.Remove(hWnd); }
                break;
        }

        return User32NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
