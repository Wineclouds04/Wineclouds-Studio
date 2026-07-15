using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WinecloudsStudio.Shared.Logging;
using WinecloudsStudio.Modules.WindowManager.Configuration;
using WinecloudsStudio.Modules.WindowManager.Services.Interop;

namespace WinecloudsStudio.Modules.WindowManager.Services.Implementation;

/// <summary>
/// Global hotkeys implemented through the reference project's RegisterHotKey / WM_HOTKEY path.
/// Standalone modifier keys use a private registered-hotkey relay so activation still runs from
/// WM_HOTKEY instead of directly from the low-level keyboard callback.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const string WindowClassName = "WinecloudsReferenceHotkeyWindow";
    private const uint ExecuteQueuedActionsMessage = 0x0401;
    private const uint ExecuteModifierRelayMessage = 0x0402;

    private static readonly object ClassLock = new();
    private static readonly ConcurrentDictionary<IntPtr, HotkeyService> Instances = new();
    private static readonly WndProc WindowProcedure = StaticWndProc;
    private static bool _classRegistered;

    private readonly ConcurrentQueue<Action> _queuedActions = new();
    private readonly Dictionary<int, (int GroupIndex, bool Forward)> _registrations = new();
    private readonly Dictionary<uint, byte> _modifierRelays = new();
    private readonly HashSet<uint> _pressedModifiers = new();
    private readonly ManualResetEventSlim _windowReady = new();
    private readonly Thread _messageThread;
    private readonly HotkeyNativeMethods.LowLevelKeyboardProc _keyboardProcedure;

    private IntPtr _windowHandle;
    private IntPtr _keyboardHook;
    private Exception? _startupException;
    private bool _disposed;

    public HotkeyService()
    {
        _keyboardProcedure = KeyboardHookCallback;
        _messageThread = new Thread(MessageLoop)
        {
            Name = "Wineclouds reference hotkeys",
            IsBackground = true
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
        _windowReady.Wait();

        if (_startupException != null)
        {
            throw new InvalidOperationException("Unable to start the global hotkey service.", _startupException);
        }
    }

    public event Action<int, bool>? HotkeyPressed;

    public void RegisterGroupHotkeys(int groupIndex, WindowGroupConfig group)
    {
        if (group.ForwardHotkey != null)
        {
            RegisterHotkey(groupIndex, true, group.ForwardHotkey);
        }

        if (group.BackwardHotkey != null)
        {
            RegisterHotkey(groupIndex, false, group.BackwardHotkey);
        }
    }

    public void UnregisterAll() => QueueOnMessageThread(UnregisterAllOnMessageThread);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        HotkeyPressed = null;

        if (_windowHandle != IntPtr.Zero)
        {
            HotkeyNativeMethods.PostMessage(
                _windowHandle,
                HotkeyNativeMethods.WM_CLOSE,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        if (_messageThread.IsAlive && !_messageThread.Join(TimeSpan.FromSeconds(3)))
        {
            Logger.Warn("HotkeyService", "Message thread did not stop within three seconds");
        }

        _windowReady.Dispose();
    }

    private void RegisterHotkey(int groupIndex, bool forward, HotkeyBinding binding)
    {
        int hotkeyId = checked(groupIndex * 2 + (forward ? 1 : 2));
        QueueOnMessageThread(() => RegisterHotkeyOnMessageThread(
            hotkeyId,
            groupIndex,
            forward,
            binding));
    }

    private void RegisterHotkeyOnMessageThread(
        int hotkeyId,
        int groupIndex,
        bool forward,
        HotkeyBinding binding)
    {
        if (_registrations.ContainsKey(hotkeyId))
        {
            HotkeyNativeMethods.UnregisterHotKey(_windowHandle, hotkeyId);
            _registrations.Remove(hotkeyId);
        }

        uint virtualKey = NormalizeVirtualKey(binding.VirtualKey);
        uint modifiers = binding.Modifiers | HotkeyNativeMethods.MOD_NOREPEAT;
        uint registeredKey = virtualKey;

        if (TryGetModifierRelay(virtualKey, out uint modifierFlag, out byte relayKey))
        {
            if (_modifierRelays.ContainsKey(virtualKey))
            {
                Logger.Warn("HotkeyService", $"Modifier key 0x{virtualKey:X} is already bound");
                return;
            }

            modifiers = modifierFlag | HotkeyNativeMethods.MOD_NOREPEAT;
            registeredKey = relayKey;
        }

        if (!HotkeyNativeMethods.RegisterHotKey(
                _windowHandle,
                hotkeyId,
                modifiers,
                registeredKey))
        {
            Logger.Warn("HotkeyService",
                $"Unable to register reference hotkey id={hotkeyId}, key=0x{virtualKey:X}; Win32 error {Marshal.GetLastWin32Error()}");
            return;
        }

        _registrations.Add(hotkeyId, (groupIndex, forward));
        if (registeredKey != virtualKey)
        {
            _modifierRelays.Add(virtualKey, (byte)registeredKey);
        }

        Logger.Info("HotkeyService",
            $"Registered reference hotkey id={hotkeyId}, group={groupIndex}, forward={forward}, modifiers=0x{modifiers:X}, key=0x{registeredKey:X}");
    }

    private void QueueOnMessageThread(Action action)
    {
        if (_disposed)
        {
            return;
        }

        _queuedActions.Enqueue(action);
        if (_windowHandle != IntPtr.Zero)
        {
            HotkeyNativeMethods.PostMessage(
                _windowHandle,
                ExecuteQueuedActionsMessage,
                IntPtr.Zero,
                IntPtr.Zero);
        }
    }

    private void DrainQueuedActions()
    {
        while (_queuedActions.TryDequeue(out Action? action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error("HotkeyService", $"Queued hotkey action failed: {ex}");
            }
        }
    }

    private void UnregisterAllOnMessageThread()
    {
        foreach (int hotkeyId in _registrations.Keys.ToArray())
        {
            HotkeyNativeMethods.UnregisterHotKey(_windowHandle, hotkeyId);
        }

        _registrations.Clear();
        _modifierRelays.Clear();
        _pressedModifiers.Clear();
    }

    private void MessageLoop()
    {
        try
        {
            EnsureWindowClass();
            _windowHandle = CreateMessageWindow();
            Instances[_windowHandle] = this;

            _keyboardHook = HotkeyNativeMethods.SetWindowsHookExW(
                HotkeyNativeMethods.WH_KEYBOARD_LL,
                _keyboardProcedure,
                HotkeyNativeMethods.GetModuleHandleW(null),
                0);
            if (_keyboardHook == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"SetWindowsHookExW failed with Win32 error {Marshal.GetLastWin32Error()}");
            }

            _windowReady.Set();
            while (HotkeyNativeMethods.GetMessage(out MSG message, IntPtr.Zero, 0, 0) > 0)
            {
                HotkeyNativeMethods.TranslateMessage(ref message);
                HotkeyNativeMethods.DispatchMessage(ref message);
            }
        }
        catch (Exception ex)
        {
            _startupException = ex;
            _windowReady.Set();
        }
        finally
        {
            RemoveKeyboardHook();
            if (_windowHandle != IntPtr.Zero)
            {
                Instances.TryRemove(_windowHandle, out _);
            }
            _windowHandle = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            uint message = unchecked((uint)wParam.ToInt64());
            var keyboardEvent = Marshal.PtrToStructure<HotkeyNativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if ((keyboardEvent.flags & HotkeyNativeMethods.LLKHF_INJECTED) == 0)
            {
                uint virtualKey = NormalizeVirtualKey(keyboardEvent.vkCode);
                if (_modifierRelays.TryGetValue(virtualKey, out byte relayKey))
                {
                    if (message is HotkeyNativeMethods.WM_KEYDOWN or HotkeyNativeMethods.WM_SYSKEYDOWN)
                    {
                        if (_pressedModifiers.Add(virtualKey))
                        {
                            HotkeyNativeMethods.PostMessage(
                                _windowHandle,
                                ExecuteModifierRelayMessage,
                                new IntPtr(unchecked((int)virtualKey)),
                                new IntPtr(relayKey));
                        }
                    }
                    else if (message is HotkeyNativeMethods.WM_KEYUP or HotkeyNativeMethods.WM_SYSKEYUP)
                    {
                        _pressedModifiers.Remove(virtualKey);
                    }
                }
            }
        }

        return HotkeyNativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void SendModifierRelay(uint modifierKey, byte relayKey)
    {
        if ((HotkeyNativeMethods.GetAsyncKeyState(modifierKey) & 0x8000) == 0)
        {
            return;
        }

        Logger.Debug("HotkeyService",
            $"Sending modifier relay key=0x{modifierKey:X}, relay=0x{relayKey:X}");
        HotkeyNativeMethods.keybd_event(relayKey, 0, 0, UIntPtr.Zero);
        HotkeyNativeMethods.keybd_event(
            relayKey,
            0,
            HotkeyNativeMethods.KEYEVENTF_KEYUP,
            UIntPtr.Zero);
    }

    private void HandleHotkey(int hotkeyId)
    {
        if (!_registrations.TryGetValue(hotkeyId, out var registration))
        {
            return;
        }

        Logger.Debug("HotkeyService",
            $"WM_HOTKEY id={hotkeyId}, group={registration.GroupIndex}, forward={registration.Forward}");
        HotkeyPressed?.Invoke(registration.GroupIndex, registration.Forward);
    }

    private void RemoveKeyboardHook()
    {
        if (_keyboardHook == IntPtr.Zero)
        {
            return;
        }

        HotkeyNativeMethods.UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
    }

    private static bool TryGetModifierRelay(
        uint virtualKey,
        out uint modifierFlag,
        out byte relayKey)
    {
        switch (virtualKey)
        {
            case HotkeyNativeMethods.VK_SHIFT:
                modifierFlag = HotkeyNativeMethods.MOD_SHIFT;
                relayKey = 0x85; // F22
                return true;
            case HotkeyNativeMethods.VK_CONTROL:
                modifierFlag = HotkeyNativeMethods.MOD_CONTROL;
                relayKey = 0x86; // F23
                return true;
            case HotkeyNativeMethods.VK_ALT:
                modifierFlag = HotkeyNativeMethods.MOD_ALT;
                relayKey = 0x87; // F24
                return true;
            case 0x5B: // Left Windows
                modifierFlag = HotkeyNativeMethods.MOD_WIN;
                relayKey = 0x84; // F21
                return true;
            case 0x5C: // Right Windows
                modifierFlag = HotkeyNativeMethods.MOD_WIN;
                relayKey = 0x83; // F20
                return true;
            default:
                modifierFlag = 0;
                relayKey = 0;
                return false;
        }
    }

    private static uint NormalizeVirtualKey(uint virtualKey) => virtualKey switch
    {
        0xA0 or 0xA1 => HotkeyNativeMethods.VK_SHIFT,
        0xA2 or 0xA3 => HotkeyNativeMethods.VK_CONTROL,
        0xA4 or 0xA5 => HotkeyNativeMethods.VK_ALT,
        _ => virtualKey
    };

    private static void EnsureWindowClass()
    {
        lock (ClassLock)
        {
            if (_classRegistered)
            {
                return;
            }

            var windowClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = WindowProcedure,
                hInstance = HotkeyNativeMethods.GetModuleHandleW(null),
                lpszClassName = WindowClassName
            };

            ushort atom = User32NativeMethods.RegisterClassEx(ref windowClass);
            if (atom == 0 && Marshal.GetLastWin32Error() != 1410)
            {
                throw new InvalidOperationException(
                    $"RegisterClassEx failed with Win32 error {Marshal.GetLastWin32Error()}");
            }

            _classRegistered = true;
        }
    }

    private static IntPtr CreateMessageWindow()
    {
        IntPtr handle = HotkeyNativeMethods.CreateWindowEx(
            0,
            WindowClassName,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            HotkeyNativeMethods.HWND_MESSAGE,
            IntPtr.Zero,
            HotkeyNativeMethods.GetModuleHandleW(null),
            IntPtr.Zero);

        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed with Win32 error {Marshal.GetLastWin32Error()}");
        }

        return handle;
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (!Instances.TryGetValue(hWnd, out HotkeyService? service))
        {
            return HotkeyNativeMethods.DefWindowProcW(hWnd, message, wParam, lParam);
        }

        switch (message)
        {
            case ExecuteQueuedActionsMessage:
                service.DrainQueuedActions();
                return IntPtr.Zero;
            case ExecuteModifierRelayMessage:
                service.SendModifierRelay(
                    unchecked((uint)wParam.ToInt64()),
                    unchecked((byte)lParam.ToInt64()));
                return IntPtr.Zero;
            case HotkeyNativeMethods.WM_HOTKEY:
                service.HandleHotkey(wParam.ToInt32());
                return IntPtr.Zero;
            case HotkeyNativeMethods.WM_CLOSE:
                service.UnregisterAllOnMessageThread();
                service.RemoveKeyboardHook();
                HotkeyNativeMethods.DestroyWindow(hWnd);
                return IntPtr.Zero;
            case HotkeyNativeMethods.WM_DESTROY:
                Instances.TryRemove(hWnd, out _);
                HotkeyNativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return HotkeyNativeMethods.DefWindowProcW(hWnd, message, wParam, lParam);
        }
    }
}
