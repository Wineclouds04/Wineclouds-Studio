using System.Runtime.InteropServices;
using WinecloudsStudio.Modules.WindowManager.Services.Interface;
using WinecloudsStudio.Modules.WindowManager.Services.Interop;

namespace WinecloudsStudio.Modules.WindowManager.Services.Implementation;

/// <summary>
/// Window management operations via Win32 API.
/// Ported from EVE-O-Preview reference project (Windows-only, simplified).
/// </summary>
public class WindowManager : IWindowManager
{
    public WindowManager()
    {
        // Composition is always enabled on Windows 8+
        IsCompositionEnabled =
            (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2)
            || Environment.OSVersion.Version.Major >= 10
            || DwmNativeMethods.DwmIsCompositionEnabled();
    }

    public bool IsCompositionEnabled { get; }

    public IntPtr GetForegroundWindowHandle()
    {
        return User32NativeMethods.GetForegroundWindow();
    }

    public void ActivateWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        User32NativeMethods.SetForegroundWindow(handle);
        User32NativeMethods.SetFocus(handle);

        uint style = User32NativeMethods.GetWindowLong(handle, InteropConstants.GWL_STYLE);

        if ((style & InteropConstants.WS_MINIMIZE) == InteropConstants.WS_MINIMIZE)
        {
            User32NativeMethods.ShowWindowAsync(handle, InteropConstants.SW_RESTORE);
        }

    }

    public void MinimizeWindow(IntPtr handle)
    {
        var placement = new WINDOWPLACEMENT();
        placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
        User32NativeMethods.GetWindowPlacement(handle, ref placement);
        placement.showCmd = WINDOWPLACEMENT.SW_MINIMIZE;
        User32NativeMethods.SetWindowPlacement(handle, ref placement);
    }

    public void MoveWindow(IntPtr handle, int left, int top, int width, int height)
    {
        User32NativeMethods.MoveWindow(handle, left, top, width, height, true);
    }

    public void MaximizeWindow(IntPtr handle)
    {
        User32NativeMethods.ShowWindowAsync(handle, InteropConstants.SW_SHOWMAXIMIZED);
    }

    public (int Left, int Top, int Right, int Bottom) GetWindowPosition(IntPtr handle)
    {
        User32NativeMethods.GetWindowRect(handle, out RECT windowRectangle);
        return (windowRectangle.Left, windowRectangle.Top, windowRectangle.Right, windowRectangle.Bottom);
    }

    public bool IsWindowMaximized(IntPtr handle)
    {
        return User32NativeMethods.IsZoomed(handle);
    }

    public bool IsWindowMinimized(IntPtr handle)
    {
        return User32NativeMethods.IsIconic(handle);
    }

    public IDwmThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source)
    {
        var thumbnail = new DwmThumbnail(this);
        thumbnail.Register(destination, source);
        return thumbnail;
    }
}
