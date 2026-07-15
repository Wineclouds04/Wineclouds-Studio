namespace WinecloudsStudio.Modules.WindowManager.Services.Interface;

/// <summary>
/// Window management operations — activate, minimize, move, get position,
/// and create DWM live thumbnails.
/// </summary>
public interface IWindowManager
{
    bool IsCompositionEnabled { get; }

    IntPtr GetForegroundWindowHandle();
    void ActivateWindow(IntPtr handle);
    void MinimizeWindow(IntPtr handle);
    void MoveWindow(IntPtr handle, int left, int top, int width, int height);
    void MaximizeWindow(IntPtr handle);
    (int Left, int Top, int Right, int Bottom) GetWindowPosition(IntPtr handle);
    bool IsWindowMaximized(IntPtr handle);
    bool IsWindowMinimized(IntPtr handle);
    IDwmThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source);
}
