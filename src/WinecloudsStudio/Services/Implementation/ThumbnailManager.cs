using System.Diagnostics;
using Microsoft.UI.Xaml;
using WinecloudsStudio.Configuration;
using WinecloudsStudio.Services.Interface;
using WinecloudsStudio.Views;

namespace WinecloudsStudio.Services.Implementation;

/// <summary>
/// Central orchestrator for window monitoring and thumbnail management.
/// Simplified port from EVE-O-Preview — no zoom, no snapping, no hotkeys.
/// Uses native Win32 windows (no WinForms dependency) for thumbnail popups.
/// </summary>
public class ThumbnailManager : IThumbnailManager
{
    private readonly IProcessMonitor _processMonitor;
    private readonly IWindowManager _windowManager;
    private readonly Dictionary<IntPtr, ThumbnailWindow> _thumbnailWindows;
    private readonly ThumbnailWindowPositionStore _positionStore;
    private readonly Dictionary<IntPtr, string> _hwndToProcessName;
    private DispatcherTimer? _updateTimer;

    private IntPtr _activeClient;

    public ThumbnailManager()
    {
        _processMonitor = new ProcessMonitor();
        _windowManager = new WindowManager();
        _thumbnailWindows = new Dictionary<IntPtr, ThumbnailWindow>();
        _positionStore = new ThumbnailWindowPositionStore();
        _hwndToProcessName = new Dictionary<IntPtr, string>();

        _activeClient = IntPtr.Zero;

        ThumbnailWidth = 280;
        ThumbnailHeight = 180;
        ThumbnailOpacity = 0.9;
        AlwaysOnTop = true;
        ShowFrames = false;
        ShowOverlayLabels = true;
    }

    // ---- Configuration properties ----

    public int ThumbnailWidth { get; set; }
    public int ThumbnailHeight { get; set; }
    public double ThumbnailOpacity { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool ShowFrames { get; set; }
    public bool ShowOverlayLabels { get; set; }
    public bool ShowBorder { get; set; }
    public bool IsRunning => _updateTimer?.IsEnabled ?? false;

    // ---- Process management ----

    public void AddMonitoredProcess(string processName)
    {
        _processMonitor.AddMonitoredProcess(processName);
    }

    public void RemoveMonitoredProcess(string processName)
    {
        _processMonitor.RemoveMonitoredProcess(processName);

        // Close thumbnails for this process
        var toRemove = _thumbnailWindows
            .Where(kvp => LookupProcessName(kvp.Key) == processName)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var handle in toRemove)
        {
            CloseThumbnail(handle);
        }
    }

    public IReadOnlyList<string> GetMonitoredProcesses()
    {
        return _processMonitor.GetMonitoredProcesses();
    }

    // ---- Start / Stop ----

    public void Start()
    {
        // If old timer exists (from a previous stop), clean it up first
        if (_updateTimer != null)
        {
            _updateTimer.Tick -= OnUpdateTimerTick;
            _updateTimer.Stop();
            _updateTimer = null;
        }

        _updateTimer = new DispatcherTimer();
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Interval = TimeSpan.FromMilliseconds(1000);
        _updateTimer.Start();
    }

    public void Stop()
    {
        // Persist window positions before tearing down
        SaveThumbnailPositions();

        if (_updateTimer != null)
        {
            _updateTimer.Tick -= OnUpdateTimerTick;
            _updateTimer.Stop();
            _updateTimer = null;
        }

        foreach (var window in _thumbnailWindows.Values.ToList())
        {
            window.Dispose();
        }
        _thumbnailWindows.Clear();
        _activeClient = IntPtr.Zero;

        // Clear process cache so next Start re-discovers all processes
        _processMonitor.ClearMonitoredProcesses();
    }

    // ---- Public query ----

    public IReadOnlyList<(IntPtr Handle, string Title)> GetActiveClients()
    {
        return _thumbnailWindows
            .Select(kvp => (kvp.Key, kvp.Value.Title))
            .ToList()
            .AsReadOnly();
    }

    // ---- Timer tick — main loop ----

    private void OnUpdateTimerTick(object? sender, object e)
    {
        UpdateThumbnailsList();
        RefreshThumbnails();
    }

    private void UpdateThumbnailsList()
    {
        _processMonitor.GetUpdatedProcesses(
            out var addedProcesses,
            out var updatedProcesses,
            out var removedProcesses);

        foreach (var process in addedProcesses)
        {
            string processName = GetProcessNameForHwnd(process.Handle);
            string windowTitle = process.Title;
            _hwndToProcessName[process.Handle] = processName;

            // Restore last saved position for this specific window
            string positionKey = MakePositionKey(processName, windowTitle);
            var savedPos = _positionStore.GetPosition(positionKey);

            var window = new ThumbnailWindow(
                process.Handle,
                processName,
                windowTitle,
                _windowManager,
                savedPos?.X,
                savedPos?.Y);

            window.OnThumbnailActivated += HandleThumbnailActivated;

            window.SetTopMost(AlwaysOnTop);
            window.SetOpacity(ThumbnailOpacity);
            window.SetThumbnailSize(ThumbnailWidth, ThumbnailHeight);
            window.Show();

            _thumbnailWindows.Add(process.Handle, window);
        }

        foreach (var process in updatedProcesses)
        {
            if (_thumbnailWindows.TryGetValue(process.Handle, out var window))
            {
                window.UpdateTitle(process.Title);
            }
        }

        foreach (var process in removedProcesses)
        {
            _hwndToProcessName.Remove(process.Handle);
            CloseThumbnail(process.Handle);
        }
    }

    private void RefreshThumbnails()
    {
        IntPtr foregroundHandle = _windowManager.GetForegroundWindowHandle();
        if (foregroundHandle == IntPtr.Zero) return;

        // Track active client
        if (foregroundHandle != _activeClient
            && _thumbnailWindows.ContainsKey(foregroundHandle))
        {
            _activeClient = foregroundHandle;
        }

        foreach (var entry in _thumbnailWindows)
        {
            var window = entry.Value;
            bool isActive = entry.Key == _activeClient;

            window.SetTopMost(AlwaysOnTop);
            window.SetOpacity(ThumbnailOpacity);
            window.SetHighlight(isActive);
            window.SetShowBorder(ShowBorder);
            window.SetThumbnailSize(ThumbnailWidth, ThumbnailHeight);
            window.RefreshThumbnail();
        }
    }

    private void HandleThumbnailActivated(IntPtr handle)
    {
        _windowManager.ActivateWindow(handle);
        _activeClient = handle;

        foreach (var entry in _thumbnailWindows)
        {
            entry.Value.SetHighlight(entry.Key == _activeClient);
        }
    }

    private void CloseThumbnail(IntPtr handle)
    {
        if (_thumbnailWindows.TryGetValue(handle, out var window))
        {
            window.OnThumbnailActivated -= HandleThumbnailActivated;
            window.Dispose();
            _thumbnailWindows.Remove(handle);
        }

        if (_activeClient == handle)
        {
            _activeClient = IntPtr.Zero;
        }
    }

    private string LookupProcessName(IntPtr handle)
    {
        if (_hwndToProcessName.TryGetValue(handle, out string? name))
            return name;
        return string.Empty;
    }

    /// <summary>
    /// Resolves a window handle to its owning process name.
    /// </summary>
    private static string GetProcessNameForHwnd(IntPtr hwnd)
    {
        _ = Services.Interop.User32NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Saves current positions of all open thumbnail windows to disk.
    /// </summary>
    private void SaveThumbnailPositions()
    {
        var positions = new Dictionary<string, (int, int)>();
        foreach (var kvp in _thumbnailWindows)
        {
            if (_hwndToProcessName.TryGetValue(kvp.Key, out string? processName)
                && !string.IsNullOrEmpty(processName))
            {
                string key = MakePositionKey(processName, kvp.Value.Title);
                positions[key] = kvp.Value.GetPosition();
            }
        }

        if (positions.Count > 0)
            _positionStore.SavePositions(positions);
    }

    /// <summary>
    /// Builds a stable storage key from process name and window title.
    /// </summary>
    private static string MakePositionKey(string processName, string windowTitle)
    {
        return $"{processName}::{windowTitle}";
    }
}
