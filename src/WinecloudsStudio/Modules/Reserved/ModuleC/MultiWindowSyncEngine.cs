using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace WinecloudsStudio.Modules.Reserved.ModuleC;

/// <summary>
/// Ports the input-forwarding boundary used by reference C to the WinUI module.
/// Physical input is observed with low-level hooks and delivered to target window
/// message queues; it never synthesizes system-wide input.
/// </summary>
internal sealed class MultiWindowSyncEngine : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int HcAction = 0;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmXButtonDown = 0x020B;
    private const uint WmXButtonUp = 0x020C;
    private const uint LlkfExtended = 0x01;
    private const uint LlkfInjected = 0x10;
    private const uint LlkfAltDown = 0x20;
    private const uint MkLButton = 0x0001;
    private const uint MkRButton = 0x0002;
    private const uint MkShift = 0x0004;
    private const uint MkControl = 0x0008;
    private const uint MkMButton = 0x0010;
    private const uint MouseButtonMask = MkLButton | MkRButton | MkMButton;
    private const int VkLButton = 0x01;
    private const int VkRButton = 0x02;
    private const int VkMButton = 0x04;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int MouseMoveThrottleMs = 8;

    private readonly NativeMethods.LowLevelKeyboardProc _keyboardCallback;
    private readonly NativeMethods.LowLevelMouseProc _mouseCallback;
    private readonly HashSet<uint> _blockedKeys = [];
    private readonly List<IntPtr> _targets = [];
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private IntPtr _primary;
    private bool _inInject;          // 防重入，匹配参考C的 m_inInject
    private long _lastMouseMoveAt;
    private bool _disposed;

    public MultiWindowSyncEngine()
    {
        _keyboardCallback = KeyboardHookCallback;
        _mouseCallback = MouseHookCallback;
    }

    public bool IsRunning { get; private set; }
    public bool KeyboardEnabled { get; set; } = true;
    public bool MouseEnabled { get; set; } = true;
    public IntPtr PrimaryWindow => _primary;
    public IReadOnlyList<IntPtr> TargetWindows => _targets.ToArray();
    public event EventHandler<string>? StatusChanged;

    public void Start(IntPtr primary, IEnumerable<IntPtr> targets, IEnumerable<uint> blockedKeys)
    {
        ThrowIfDisposed();
        Stop();

        List<IntPtr> distinctTargets = targets
            .Where(NativeMethods.IsWindow)
            .Where(handle => handle != primary)
            .Distinct()
            .ToList();

        if (!NativeMethods.IsWindow(primary))
            throw new InvalidOperationException("主控窗口已失效，请刷新列表后重新选择。");
        if (distinctTargets.Count == 0)
            throw new InvalidOperationException("请至少选择一个受控窗口。");

        _primary = primary;
        _targets.Clear();
        _targets.AddRange(distinctTargets);
        _blockedKeys.Clear();
        _blockedKeys.UnionWith(blockedKeys);

        IntPtr module = NativeMethods.GetModuleHandleW(null);
        if (module == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            Stop();
            throw new Win32Exception(error, "无法获取当前程序模块句柄。");
        }

        _keyboardHook = NativeMethods.SetKeyboardHook(WhKeyboardLl, _keyboardCallback, module, 0);
        _mouseHook = NativeMethods.SetMouseHook(WhMouseLl, _mouseCallback, module, 0);
        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            Stop();
            throw new Win32Exception(error, "无法安装全局键鼠钩子。请确认程序以管理员身份运行。");
        }

        _inInject = false;
        _lastMouseMoveAt = 0;
        IsRunning = true;
        Report("同步中：切换到主控窗口后开始操作。");
    }

    public void Stop()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        bool wasRunning = IsRunning;
        IsRunning = false;
        _inInject = false;
        _primary = IntPtr.Zero;
        _targets.Clear();
        if (wasRunning)
            Report("同步已停止。");
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code != HcAction || !IsRunning)
            return NativeMethods.CallNextHookEx(_keyboardHook, code, wParam, lParam);

        if (_inInject)
            return NativeMethods.CallNextHookEx(_keyboardHook, code, wParam, lParam);

        uint message = unchecked((uint)wParam.ToInt64());
        if (message is WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp)
        {
            NativeMethods.KbdLlHookStruct input = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            if ((input.Flags & LlkfInjected) == 0 && ShouldForwardKeyboard() && !_blockedKeys.Contains(input.VirtualKey))
                ForwardKeyboard(message, input);
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private bool ShouldForwardKeyboard() =>
        KeyboardEnabled && NativeMethods.IsWindow(_primary) && NativeMethods.GetForegroundWindow() == _primary;

    private void ForwardKeyboard(uint message, NativeMethods.KbdLlHookStruct input)
    {
        long lParam = 1 | ((long)(input.ScanCode & 0xFF) << 16);
        if ((input.Flags & LlkfExtended) != 0) lParam |= 1L << 24;
        if ((input.Flags & LlkfAltDown) != 0) lParam |= 1L << 29;
        if (message is WmKeyUp or WmSysKeyUp) lParam |= (1L << 30) | (1L << 31);

        foreach (IntPtr target in GetValidTargets())
            NativeMethods.PostMessage(target, message, new IntPtr(input.VirtualKey), new IntPtr(lParam));
    }

    // ── Mouse hook callback (ported from reference C) ─────────────

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code != HcAction || !IsRunning)
            return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);

        if (_inInject)
            return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);

        var input = Marshal.PtrToStructure<NativeMethods.MouseLlHookStruct>(lParam);
        int sx = input.Point.X;
        int sy = input.Point.Y;

        if (!ShouldForwardMouse(sx, sy))
            return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);

        uint message = unchecked((uint)wParam.ToInt64());

        switch (message)
        {
            case WmMouseMove:
            {
                long now = Environment.TickCount64;
                if (now - _lastMouseMoveAt >= MouseMoveThrottleMs)
                {
                    _lastMouseMoveAt = now;
                    InjectMouseMove(sx, sy);
                }
                break;
            }
            case WmLButtonDown: case WmLButtonUp:
            case WmRButtonDown: case WmRButtonUp:
            case WmMButtonDown: case WmMButtonUp:
                InjectMouseButton(message, sx, sy);
                break;
            case WmMouseWheel:
                InjectMouseWheel((int)(input.MouseData >> 16), sx, sy);
                break;
            case WmXButtonDown: case WmXButtonUp:
                InjectMouseButton(message, sx, sy);
                break;
        }

        return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    // ── Guard helpers ──────────────────────────────────────────────

    private bool ShouldForwardMouse(int screenX, int screenY)
    {
        if (!MouseEnabled || !NativeMethods.IsWindow(_primary))
            return false;
        if (NativeMethods.GetForegroundWindow() != _primary)
            return false;
        return PointInPrimaryClient(screenX, screenY);
    }

    private bool PointInPrimaryClient(int screenX, int screenY)
    {
        if (!NativeMethods.GetClientRect(_primary, out NativeMethods.Rect client))
            return false;

        var origin = new NativeMethods.Point();
        if (!NativeMethods.ClientToScreen(_primary, ref origin))
            return false;

        int left = origin.X;
        int top = origin.Y;
        int right = left + client.Width;
        int bottom = top + client.Height;

        return screenX >= left && screenX < right
            && screenY >= top && screenY < bottom;
    }

    // ── Injectors (ported from reference C) ───────────────────────

    private IntPtr ScreenToClientLParam(IntPtr hWnd, int screenX, int screenY)
    {
        var pt = new NativeMethods.Point { X = screenX, Y = screenY };
        NativeMethods.ScreenToClient(hWnd, ref pt);
        int packed = (ushort)pt.X | ((int)(ushort)pt.Y << 16);
        return new IntPtr(packed);
    }

    private void InjectMouseMove(int screenX, int screenY)
    {
        _inInject = true;

        uint wp = 0;
        if ((NativeMethods.GetAsyncKeyState(VkLButton) & 0x8000) != 0) wp |= MkLButton;
        if ((NativeMethods.GetAsyncKeyState(VkRButton) & 0x8000) != 0) wp |= MkRButton;
        if ((NativeMethods.GetAsyncKeyState(VkMButton) & 0x8000) != 0) wp |= MkMButton;
        if ((NativeMethods.GetAsyncKeyState(VkControl) & 0x8000) != 0) wp |= MkControl;
        if ((NativeMethods.GetAsyncKeyState(VkShift)   & 0x8000) != 0) wp |= MkShift;

        foreach (IntPtr child in GetValidTargets())
        {
            IntPtr lp = ScreenToClientLParam(child, screenX, screenY);
            NativeMethods.PostMessage(child, WmMouseMove, new IntPtr(wp), lp);
        }

        _inInject = false;
    }

    private void InjectMouseButton(uint msg, int screenX, int screenY)
    {
        _inInject = true;

        uint wp = msg switch
        {
            WmLButtonDown => MkLButton,
            WmRButtonDown => MkRButton,
            WmMButtonDown => MkMButton,
            _ => 0
        };
        if ((NativeMethods.GetAsyncKeyState(VkControl) & 0x8000) != 0) wp |= MkControl;
        if ((NativeMethods.GetAsyncKeyState(VkShift)   & 0x8000) != 0) wp |= MkShift;

        foreach (IntPtr child in GetValidTargets())
        {
            IntPtr lp = ScreenToClientLParam(child, screenX, screenY);
            NativeMethods.PostMessage(child, msg, new IntPtr(wp), lp);
        }

        _inInject = false;
    }

    private void InjectMouseWheel(int delta, int screenX, int screenY)
    {
        _inInject = true;

        uint wp = (uint)(delta << 16);
        if ((NativeMethods.GetAsyncKeyState(VkControl) & 0x8000) != 0) wp |= MkControl;
        if ((NativeMethods.GetAsyncKeyState(VkShift)   & 0x8000) != 0) wp |= MkShift;

        foreach (IntPtr child in GetValidTargets())
        {
            IntPtr lp = ScreenToClientLParam(child, screenX, screenY);
            NativeMethods.PostMessage(child, WmMouseWheel, new IntPtr(wp), lp);
        }

        _inInject = false;
    }

    private IEnumerable<IntPtr> GetValidTargets() => _targets.Where(NativeMethods.IsWindow).ToArray();

    private void Report(string text) => StatusChanged?.Invoke(this, text);

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MultiWindowSyncEngine));
    }
}

internal sealed class SyncWindowItem : INotifyPropertyChanged
{
    private bool _isPrimary;
    private bool _isTarget;

    public required IntPtr Handle { get; init; }
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public string DisplayText => $"{ProcessName}  —  {WindowTitle}  (PID {ProcessId})";
    public bool CanBeTarget => !IsPrimary;

    public bool IsPrimary
    {
        get => _isPrimary;
        set
        {
            if (_isPrimary == value) return;
            _isPrimary = value;
            if (value) IsTarget = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanBeTarget));
        }
    }

    public bool IsTarget
    {
        get => _isTarget;
        set
        {
            if (_isTarget == value) return;
            _isTarget = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal static class SyncWindowEnumerator
{
    private const long WsChild = 0x40000000;
    private const long WsExToolWindow = 0x00000080;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;

    public static List<SyncWindowItem> GetCandidates()
    {
        int currentProcessId = Environment.ProcessId;
        var items = new List<SyncWindowItem>();

        NativeMethods.EnumWindows((handle, _) =>
        {
            try
            {
                if (!IsCandidate(handle)) return true;

                NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
                if (processId == currentProcessId) return true;

                using Process process = Process.GetProcessById((int)processId);
                items.Add(new SyncWindowItem
                {
                    Handle = handle,
                    ProcessId = (int)processId,
                    ProcessName = process.ProcessName,
                    WindowTitle = NativeMethods.GetWindowText(handle)
                });
            }
            catch
            {
                // Never allow an exception to cross the native callback boundary.
            }

            return true;
        }, IntPtr.Zero);

        return items
            .OrderBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.WindowTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProcessId)
            .ThenBy(item => item.Handle.ToInt64())
            .ToList();
    }

    private static bool IsCandidate(IntPtr handle)
    {
        if (!NativeMethods.IsWindowVisible(handle) || NativeMethods.IsIconic(handle)) return false;
        if ((NativeMethods.GetWindowLongPtr(handle, GwlStyle).ToInt64() & WsChild) != 0) return false;
        if ((NativeMethods.GetWindowLongPtr(handle, GwlExStyle).ToInt64() & WsExToolWindow) != 0) return false;
        if (string.IsNullOrWhiteSpace(NativeMethods.GetWindowText(handle))) return false;
        if (!NativeMethods.GetWindowRect(handle, out NativeMethods.Rect rect)) return false;
        return rect.Width >= 200 && rect.Height >= 150;
    }
}

internal static class NativeMethods
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left; public int Top; public int Right; public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct
    {
        public uint VirtualKey; public uint ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseLlHookStruct
    {
        public Point Point; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int index);
    internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int index) => GetWindowLongPtrW(hWnd, index);
    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")]
    internal static extern int GetWindowTextLengthW(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hWnd, ref Point point);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ScreenToClient(IntPtr hWnd, ref Point point);
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static extern IntPtr SetKeyboardHook(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static extern IntPtr SetMouseHook(int idHook, LowLevelMouseProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandleW(string? moduleName);

    internal static string GetWindowText(IntPtr handle)
    {
        int length = GetWindowTextLengthW(handle);
        if (length <= 0) return string.Empty;
        var text = new StringBuilder(length + 1);
        GetWindowTextW(handle, text, text.Capacity);
        return text.ToString();
    }
}
