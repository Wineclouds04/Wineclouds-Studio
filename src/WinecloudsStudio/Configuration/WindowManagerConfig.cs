namespace WinecloudsStudio.Configuration;

/// <summary>
/// Configuration for the Window Manager module.
/// Holds user-adjustable settings for thumbnail display.
/// </summary>
public class WindowManagerConfig
{
    public int ThumbnailWidth { get; set; } = 280;
    public int ThumbnailHeight { get; set; } = 180;
    public double ThumbnailOpacity { get; set; } = 0.9;
    public bool AlwaysOnTop { get; set; } = true;
    public bool LockThumbnailPosition { get; set; } = false;
    public bool SnapThumbnailsToGrid { get; set; } = false;
    public bool ShowThumbnailFrames { get; set; } = false;
    public bool ShowBorder { get; set; } = false;
    public bool ShowOverlayLabels { get; set; } = true;
    public List<string> MonitoredProcesses { get; set; } = new();
    public List<WindowGroupConfig> Groups { get; set; } = new();
}
