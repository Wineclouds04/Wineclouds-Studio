using System.Runtime.InteropServices;

namespace WinecloudsStudio.Services.Interop;

/// <summary>
/// P/Invoke declarations for global hotkey registration via user32.dll.
/// </summary>
public static class HotkeyNativeMethods
{
    public const int WH_KEYBOARD_LL = 13;
    public const uint LLKHF_INJECTED = 0x00000010;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void keybd_event(
        byte bVk,
        byte bScan,
        uint dwFlags,
        UIntPtr dwExtraInfo);

    /// <summary>Registers a system-wide hotkey.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>Unregisters a previously registered hotkey.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Returns the state of a virtual key. Bit 15 = key is currently down.</summary>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(uint vKey);

    // ---- Message pump APIs ----

    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    // ---- Window creation (message-only) ----

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
        uint dwStyle,
        int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    // ---- Sentinels and constants ----

    /// <summary>Use as parent HWND to create a message-only window.</summary>
    public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_CLOSE = 0x0010;

    // ---- Modifier flags ----
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // ---- Common virtual-key codes ----
    public const uint VK_BACK = 0x08;
    public const uint VK_TAB = 0x09;
    public const uint VK_RETURN = 0x0D;
    public const uint VK_SHIFT = 0x10;
    public const uint VK_CONTROL = 0x11;
    public const uint VK_ALT = 0x12;
    public const uint VK_PAUSE = 0x13;
    public const uint VK_CAPITAL = 0x14;
    public const uint VK_ESCAPE = 0x1B;
    public const uint VK_SPACE = 0x20;
    public const uint VK_PRIOR = 0x21;
    public const uint VK_NEXT = 0x22;
    public const uint VK_END = 0x23;
    public const uint VK_HOME = 0x24;
    public const uint VK_LEFT = 0x25;
    public const uint VK_UP = 0x26;
    public const uint VK_RIGHT = 0x27;
    public const uint VK_DOWN = 0x28;
    public const uint VK_INSERT = 0x2D;
    public const uint VK_DELETE = 0x2E;

    public const uint VK_0 = 0x30;
    public const uint VK_1 = 0x31;
    public const uint VK_2 = 0x32;
    public const uint VK_3 = 0x33;
    public const uint VK_4 = 0x34;
    public const uint VK_5 = 0x35;
    public const uint VK_6 = 0x36;
    public const uint VK_7 = 0x37;
    public const uint VK_8 = 0x38;
    public const uint VK_9 = 0x39;

    public const uint VK_A = 0x41;
    public const uint VK_B = 0x42;
    public const uint VK_C = 0x43;
    public const uint VK_D = 0x44;
    public const uint VK_E = 0x45;
    public const uint VK_F = 0x46;
    public const uint VK_G = 0x47;
    public const uint VK_H = 0x48;
    public const uint VK_I = 0x49;
    public const uint VK_J = 0x4A;
    public const uint VK_K = 0x4B;
    public const uint VK_L = 0x4C;
    public const uint VK_M = 0x4D;
    public const uint VK_N = 0x4E;
    public const uint VK_O = 0x4F;
    public const uint VK_P = 0x50;
    public const uint VK_Q = 0x51;
    public const uint VK_R = 0x52;
    public const uint VK_S = 0x53;
    public const uint VK_T = 0x54;
    public const uint VK_U = 0x55;
    public const uint VK_V = 0x56;
    public const uint VK_W = 0x57;
    public const uint VK_X = 0x58;
    public const uint VK_Y = 0x59;
    public const uint VK_Z = 0x5A;

    public const uint VK_F1 = 0x70;
    public const uint VK_F2 = 0x71;
    public const uint VK_F3 = 0x72;
    public const uint VK_F4 = 0x73;
    public const uint VK_F5 = 0x74;
    public const uint VK_F6 = 0x75;
    public const uint VK_F7 = 0x76;
    public const uint VK_F8 = 0x77;
    public const uint VK_F9 = 0x78;
    public const uint VK_F10 = 0x79;
    public const uint VK_F11 = 0x7A;
    public const uint VK_F12 = 0x7B;

    public const uint VK_NUMPAD0 = 0x60;
    public const uint VK_NUMPAD1 = 0x61;
    public const uint VK_NUMPAD2 = 0x62;
    public const uint VK_NUMPAD3 = 0x63;
    public const uint VK_NUMPAD4 = 0x64;
    public const uint VK_NUMPAD5 = 0x65;
    public const uint VK_NUMPAD6 = 0x66;
    public const uint VK_NUMPAD7 = 0x67;
    public const uint VK_NUMPAD8 = 0x68;
    public const uint VK_NUMPAD9 = 0x69;

    public const uint VK_OEM_PLUS = 0xBB;
    public const uint VK_OEM_MINUS = 0xBD;
    public const uint VK_OEM_PERIOD = 0xBE;
    public const uint VK_OEM_COMMA = 0xBC;
    public const uint VK_OEM_1 = 0xBA;
    public const uint VK_OEM_2 = 0xBF;
    public const uint VK_OEM_3 = 0xC0;
    public const uint VK_OEM_4 = 0xDB;
    public const uint VK_OEM_5 = 0xDC;
    public const uint VK_OEM_6 = 0xDD;
    public const uint VK_OEM_7 = 0xDE;
}
