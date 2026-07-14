namespace WinecloudsStudio.Configuration;

/// <summary>
/// A user-configurable hotkey binding for window cycling.
/// </summary>
public class HotkeyBinding
{
    /// <summary>
    /// Win32 modifier flags: MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8.
    /// </summary>
    public uint Modifiers { get; set; }

    /// <summary>
    /// Win32 virtual-key code (e.g. VK_F1=0x70, VK_TAB=0x09).
    /// </summary>
    public uint VirtualKey { get; set; }
}

/// <summary>
/// A named, ordered group of windows that can be cycled via hotkeys.
/// Persisted as part of <see cref="WindowManagerConfig"/>.
/// </summary>
public class WindowGroupConfig
{
    /// <summary>User-facing group label.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of window keys in cycle order.
    /// Each key is "processName::windowTitle".
    /// </summary>
    public List<string> WindowKeys { get; set; } = new();

    /// <summary>Hotkey that cycles to the next window in the group.</summary>
    public HotkeyBinding? ForwardHotkey { get; set; }

    /// <summary>Hotkey that cycles to the previous window in the group.</summary>
    public HotkeyBinding? BackwardHotkey { get; set; }
}
