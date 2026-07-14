namespace WinecloudsStudio.Services.Interface;

/// <summary>
/// Central orchestrator for the window manager module.
/// Manages process monitoring, thumbnail lifecycle, and window switching.
/// </summary>
public interface IThumbnailManager
{
    void AddMonitoredProcess(string processName);
    void RemoveMonitoredProcess(string processName);
    IReadOnlyList<string> GetMonitoredProcesses();

    void Start();
    void Stop();
    bool IsRunning { get; }

    int ThumbnailWidth { get; set; }
    int ThumbnailHeight { get; set; }
    double ThumbnailOpacity { get; set; }
    bool AlwaysOnTop { get; set; }
    bool ShowFrames { get; set; }
    bool ShowOverlayLabels { get; set; }
    bool ShowBorder { get; set; }
}
