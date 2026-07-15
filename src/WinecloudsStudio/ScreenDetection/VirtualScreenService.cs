using System.Runtime.InteropServices;
using WinecloudsStudio.Detection;

namespace WinecloudsStudio.ScreenDetection;

public static class VirtualScreenService
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public static ScreenRegion GetBounds() => new(
        GetSystemMetrics(SmXVirtualScreen),
        GetSystemMetrics(SmYVirtualScreen),
        GetSystemMetrics(SmCxVirtualScreen),
        GetSystemMetrics(SmCyVirtualScreen));

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
