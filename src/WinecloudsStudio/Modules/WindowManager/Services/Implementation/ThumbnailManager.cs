using System.Diagnostics;
using System.Drawing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinecloudsStudio.Shared.Logging;
using WinecloudsStudio.Modules.WindowManager.Configuration;
using WinecloudsStudio.Modules.WindowManager.Services.Interface;
using WinecloudsStudio.Modules.WindowManager.Services.Interop;
using WinecloudsStudio.Modules.WindowManager.Views;

namespace WinecloudsStudio.Modules.WindowManager.Services.Implementation;

public sealed class ThumbnailManager : IThumbnailManager
{
    private readonly IProcessMonitor _processMonitor;
    private readonly IWindowManager _windowManager;
    private readonly ThumbnailViewFactory _viewFactory;
    private readonly ThumbnailWindowPositionStore _positionStore;
    private readonly Dictionary<IntPtr, IThumbnailView> _thumbnailViews = new();
    private readonly Dictionary<IntPtr, string> _processNames = new();
    private readonly Dictionary<IntPtr, string> _windowKeys = new();
    private readonly DispatcherQueue _dispatcher;

    private DispatcherTimer? _updateTimer;
    private HotkeyService? _hotkeyService;
    private List<WindowGroupConfig> _groups = new();
    private IntPtr _activeClient;
    private bool _snapThumbnailsToGrid;

    public ThumbnailManager()
    {
        _processMonitor = new ProcessMonitor();
        _windowManager = new WindowManager();
        _viewFactory = new ThumbnailViewFactory(_windowManager);
        _positionStore = new ThumbnailWindowPositionStore();
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        ThumbnailWidth = 280;
        ThumbnailHeight = 180;
        ThumbnailOpacity = 0.9;
        AlwaysOnTop = true;
        ShowOverlayLabels = true;
    }

    public int ThumbnailWidth { get; set; }
    public int ThumbnailHeight { get; set; }
    public double ThumbnailOpacity { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool LockThumbnailPosition { get; set; }
    public bool SnapThumbnailsToGrid
    {
        get => _snapThumbnailsToGrid;
        set
        {
            _snapThumbnailsToGrid = value;
            foreach (IThumbnailView view in _thumbnailViews.Values)
            {
                view.SetSnapToGrid(value);
            }
        }
    }
    public bool ShowFrames { get; set; }
    public bool ShowOverlayLabels { get; set; }
    public bool ShowBorder { get; set; }
    public bool IsRunning => _updateTimer?.IsEnabled ?? false;
    public IReadOnlyList<WindowGroupConfig> Groups => _groups.AsReadOnly();

    public void AddMonitoredProcess(string processName) =>
        _processMonitor.AddMonitoredProcess(processName);

    public void RemoveMonitoredProcess(string processName)
    {
        _processMonitor.RemoveMonitoredProcess(processName);
        foreach (IntPtr handle in _thumbnailViews
                     .Where(entry => GetCachedProcessName(entry.Key)
                         .Equals(processName, StringComparison.OrdinalIgnoreCase))
                     .Select(entry => entry.Key)
                     .ToList())
        {
            CloseThumbnail(handle);
        }
    }

    public IReadOnlyList<string> GetMonitoredProcesses() =>
        _processMonitor.GetMonitoredProcesses();

    public void Start()
    {
        StopTimer();

        _hotkeyService?.Dispose();
        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += HotkeyPressed;
        RegisterGroupHotkeys();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimerTick;
        _updateTimer.Start();

        UpdateThumbnailsList();
        RefreshThumbnails();
        Logger.Info("ThumbnailManager", $"Monitoring started with {_groups.Count} groups");
    }

    public void Stop()
    {
        SaveThumbnailPositions();
        StopTimer();

        if (_hotkeyService != null)
        {
            _hotkeyService.HotkeyPressed -= HotkeyPressed;
            _hotkeyService.Dispose();
            _hotkeyService = null;
        }

        foreach (IntPtr handle in _thumbnailViews.Keys.ToList())
        {
            CloseThumbnail(handle);
        }

        _processNames.Clear();
        _windowKeys.Clear();
        _processMonitor.ClearMonitoredProcesses();
        _activeClient = IntPtr.Zero;
        Logger.Info("ThumbnailManager", "Monitoring stopped");
    }

    public IReadOnlyList<(IntPtr Handle, string Title)> GetActiveClients() =>
        _thumbnailViews.Select(entry => (entry.Key, entry.Value.Title)).ToList();

    public void SetGroups(IReadOnlyList<WindowGroupConfig> groups)
    {
        _groups = groups.ToList();
        if (_hotkeyService == null)
        {
            return;
        }

        _hotkeyService.UnregisterAll();
        RegisterGroupHotkeys();
    }

    public void CycleGroupForward(int groupIndex) => CycleGroup(groupIndex, true);

    public void CycleGroupBackward(int groupIndex) => CycleGroup(groupIndex, false);

    private void UpdateTimerTick(object? sender, object e)
    {
        try
        {
            UpdateThumbnailsList();
            RefreshThumbnails();
        }
        catch (Exception ex)
        {
            Logger.Error("ThumbnailManager", $"Refresh failed: {ex}");
        }
    }

    private void UpdateThumbnailsList()
    {
        _processMonitor.GetUpdatedProcesses(
            out var addedProcesses,
            out var updatedProcesses,
            out var removedProcesses);

        foreach (IProcessInfo process in addedProcesses)
        {
            if (_thumbnailViews.ContainsKey(process.Handle))
            {
                continue;
            }

            string processName = ResolveProcessName(process.Handle);
            _processNames[process.Handle] = processName;
            string positionKey = MakeWindowKey(processName, process.Title);
            _windowKeys[process.Handle] = positionKey;
            (int X, int Y)? savedPosition = _positionStore.GetPosition(positionKey);
            var position = savedPosition.HasValue
                ? new Point(savedPosition.Value.X, savedPosition.Value.Y)
                : GetDefaultPosition();

            IThumbnailView view = _viewFactory.Create(
                process.Handle,
                processName,
                process.Title,
                new Size(ThumbnailWidth, ThumbnailHeight),
                position);

            WireView(view);
            ApplyViewSettings(view, forceRefresh: false);
            _thumbnailViews.Add(process.Handle, view);
            view.ShowThumbnail();
        }

        foreach (IProcessInfo process in updatedProcesses)
        {
            if (_thumbnailViews.TryGetValue(process.Handle, out IThumbnailView? view))
            {
                view.Title = process.Title;
            }
        }

        foreach (IProcessInfo process in removedProcesses)
        {
            CloseThumbnail(process.Handle);
        }
    }

    private void RefreshThumbnails()
    {
        IntPtr foregroundClient = ResolveForegroundClientHandle();
        if (foregroundClient != IntPtr.Zero)
        {
            _activeClient = foregroundClient;
        }

        foreach (IThumbnailView view in _thumbnailViews.Values)
        {
            ApplyViewSettings(view, forceRefresh: false);
        }
    }

    private void ApplyViewSettings(IThumbnailView view, bool forceRefresh)
    {
        Size configuredSize = new(ThumbnailWidth, ThumbnailHeight);
        if (view.ThumbnailSize != configuredSize)
        {
            view.ThumbnailSize = configuredSize;
            forceRefresh = true;
        }

        view.SetTopMost(AlwaysOnTop);
        view.SetPositionLocked(LockThumbnailPosition);
        view.SetSnapToGrid(SnapThumbnailsToGrid);
        view.SetOpacity(ThumbnailOpacity);
        view.SetOverlayLabel(ShowOverlayLabels);
        view.SetShowBorder(ShowBorder);
        view.SetHighlight(view.Id == _activeClient);
        view.RefreshThumbnail(forceRefresh);
    }

    private void WireView(IThumbnailView view)
    {
        view.ThumbnailActivated += ThumbnailActivated;
        view.ThumbnailMoved += ThumbnailMoved;
    }

    private void UnwireView(IThumbnailView view)
    {
        view.ThumbnailActivated -= ThumbnailActivated;
        view.ThumbnailMoved -= ThumbnailMoved;
    }

    private void ThumbnailActivated(IntPtr id)
    {
        if (!_thumbnailViews.ContainsKey(id))
        {
            return;
        }

        if (!_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(() => ActivateView(id));
            return;
        }

        ActivateView(id);
    }

    private void ActivateView(IntPtr id)
    {
        if (!_thumbnailViews.ContainsKey(id))
        {
            return;
        }

        _windowManager.ActivateWindow(id);

        _activeClient = id;
        foreach (IThumbnailView view in _thumbnailViews.Values)
        {
            view.SetHighlight(view.Id == id);
            view.RefreshThumbnail(false);
        }
    }

    private void ThumbnailMoved(IntPtr id)
    {
        if (_thumbnailViews.TryGetValue(id, out IThumbnailView? view))
        {
            Logger.Debug("ThumbnailManager",
                $"Thumbnail moved: {view.ProcessName}::{view.Title} -> {view.ThumbnailLocation.X},{view.ThumbnailLocation.Y}");
        }
    }

    private void CycleGroup(int groupIndex, bool forward)
    {
        if ((uint)groupIndex >= (uint)_groups.Count)
        {
            return;
        }

        List<IntPtr> orderedHandles = ResolveGroupHandles(_groups[groupIndex]);
        if (orderedHandles.Count == 0)
        {
            return;
        }

        IntPtr foregroundClient = ResolveForegroundClientHandle();
        if (foregroundClient != IntPtr.Zero)
        {
            _activeClient = foregroundClient;
        }

        int currentIndex = orderedHandles.IndexOf(_activeClient);
        int targetIndex;
        if (currentIndex < 0)
        {
            targetIndex = forward ? 0 : orderedHandles.Count - 1;
        }
        else
        {
            targetIndex = forward
                ? (currentIndex + 1) % orderedHandles.Count
                : (currentIndex - 1 + orderedHandles.Count) % orderedHandles.Count;
        }

        ThumbnailActivated(orderedHandles[targetIndex]);
    }

    private IntPtr ResolveForegroundClientHandle()
    {
        IntPtr foregroundHandle = _windowManager.GetForegroundWindowHandle();
        if (foregroundHandle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (_thumbnailViews.ContainsKey(foregroundHandle))
        {
            return foregroundHandle;
        }

        _ = User32NativeMethods.GetWindowThreadProcessId(
            foregroundHandle,
            out uint foregroundProcessId);
        if (foregroundProcessId == 0)
        {
            return IntPtr.Zero;
        }

        foreach (IntPtr clientHandle in _thumbnailViews.Keys)
        {
            _ = User32NativeMethods.GetWindowThreadProcessId(
                clientHandle,
                out uint clientProcessId);
            if (clientProcessId == foregroundProcessId)
            {
                return clientHandle;
            }
        }

        return IntPtr.Zero;
    }

    private List<IntPtr> ResolveGroupHandles(WindowGroupConfig group)
    {
        var handles = new List<IntPtr>();
        foreach (string configuredKey in group.WindowKeys)
        {
            KeyValuePair<IntPtr, string>? match = _windowKeys
                .FirstOrDefault(entry => entry.Value
                    .Equals(configuredKey, StringComparison.OrdinalIgnoreCase));

            if (match.HasValue && match.Value.Key != IntPtr.Zero && !handles.Contains(match.Value.Key))
            {
                handles.Add(match.Value.Key);
            }
        }

        return handles;
    }

    private void HotkeyPressed(int groupIndex, bool forward)
    {
        if (_dispatcher.HasThreadAccess)
        {
            CycleGroup(groupIndex, forward);
            return;
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool queued = _dispatcher.TryEnqueue(() =>
        {
            try
            {
                CycleGroup(groupIndex, forward);
            }
            finally
            {
                completion.TrySetResult(true);
            }
        });

        if (!queued)
        {
            Logger.Warn("ThumbnailManager", "Unable to enqueue group hotkey switch");
            return;
        }

        // Keep the low-level keyboard callback active until the reference-style
        // SetActive path has handled this physical key event.
        if (!completion.Task.Wait(TimeSpan.FromMilliseconds(250)))
        {
            Logger.Warn("ThumbnailManager", "Group hotkey switch exceeded 250 ms");
        }
    }

    private void RegisterGroupHotkeys()
    {
        if (_hotkeyService == null)
        {
            return;
        }

        for (int index = 0; index < _groups.Count; index++)
        {
            _hotkeyService.RegisterGroupHotkeys(index, _groups[index]);
        }
    }

    private void CloseThumbnail(IntPtr handle)
    {
        if (!_thumbnailViews.Remove(handle, out IThumbnailView? view))
        {
            _processNames.Remove(handle);
            return;
        }

        UnwireView(view);
        view.Dispose();
        _processNames.Remove(handle);
        _windowKeys.Remove(handle);
        if (_activeClient == handle)
        {
            _activeClient = IntPtr.Zero;
        }
    }

    private void SaveThumbnailPositions()
    {
        var positions = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        foreach (IThumbnailView view in _thumbnailViews.Values)
        {
            string key = _windowKeys.TryGetValue(view.Id, out string? stableKey)
                ? stableKey
                : MakeWindowKey(view.ProcessName, view.Title);
            positions[key] =
                (view.ThumbnailLocation.X, view.ThumbnailLocation.Y);
        }

        if (positions.Count > 0)
        {
            _positionStore.SavePositions(positions);
        }
    }

    private Point GetDefaultPosition()
    {
        int offset = _thumbnailViews.Count * 30;
        return new Point(100 + offset, 100 + offset);
    }

    private void StopTimer()
    {
        if (_updateTimer == null)
        {
            return;
        }

        _updateTimer.Tick -= UpdateTimerTick;
        _updateTimer.Stop();
        _updateTimer = null;
    }

    private string GetCachedProcessName(IntPtr handle) =>
        _processNames.TryGetValue(handle, out string? name) ? name : string.Empty;

    private static string ResolveProcessName(IntPtr handle)
    {
        _ = User32NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string MakeWindowKey(string processName, string title) =>
        $"{processName}::{title}";
}
