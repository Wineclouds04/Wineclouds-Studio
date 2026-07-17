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
    // EVE-O-Preview treats the shared EVE login title as a special multi-instance client.
    private const string DefaultClientTitle = "EVE";

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
    private string _activeClientTitle = DefaultClientTitle;
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
        _activeClientTitle = DefaultClientTitle;
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
        if (foregroundClient != IntPtr.Zero
            && _thumbnailViews.TryGetValue(foregroundClient, out IThumbnailView? foregroundView))
        {
            SwitchActiveClient(foregroundClient, foregroundView.Title);
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
        view.ThumbnailCycleGroupToggled += ThumbnailCycleGroupToggled;
    }

    private void UnwireView(IThumbnailView view)
    {
        view.ThumbnailActivated -= ThumbnailActivated;
        view.ThumbnailMoved -= ThumbnailMoved;
        view.ThumbnailCycleGroupToggled -= ThumbnailCycleGroupToggled;
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
        if (!_thumbnailViews.TryGetValue(id, out IThumbnailView? view))
        {
            return;
        }

        SetActive(view);
    }

    private void SetActive(IThumbnailView view)
    {
        _windowManager.ActivateWindow(view.Id);
        SwitchActiveClient(view.Id, view.Title);
        foreach (IThumbnailView thumbnailView in _thumbnailViews.Values)
        {
            thumbnailView.SetHighlight(thumbnailView.Id == _activeClient);
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

    private void ThumbnailCycleGroupToggled(IntPtr id)
    {
        if (!_thumbnailViews.TryGetValue(id, out IThumbnailView? view))
        {
            return;
        }

        view.IsExcludedFromCycleGroup = !view.IsExcludedFromCycleGroup;
        Logger.Info("ThumbnailManager",
            $"Cycle group exclusion changed: {view.ProcessName}::{view.Title} -> {view.IsExcludedFromCycleGroup}");
    }

    private void CycleGroup(int groupIndex, bool forward)
    {
        if ((uint)groupIndex >= (uint)_groups.Count)
        {
            return;
        }

        List<string> clientOrder = ResolveCycleOrder(_groups[groupIndex]);
        if (clientOrder.Count == 0)
        {
            Logger.Warn("ThumbnailManager", $"Cycle group {groupIndex} has no available windows");
            return;
        }

        Logger.Debug("ThumbnailManager",
            $"Cycle group={groupIndex}, forward={forward}, active={_activeClientTitle}, order={string.Join(" -> ", clientOrder)}");

        IntPtr foregroundClient = ResolveForegroundClientHandle();
        if (foregroundClient != IntPtr.Zero
            && _thumbnailViews.TryGetValue(foregroundClient, out IThumbnailView? foregroundView))
        {
            SwitchActiveClient(foregroundClient, foregroundView.Title);
        }

        IEnumerable<string> orderedTitles = forward
            ? clientOrder
            : clientOrder.AsEnumerable().Reverse();
        bool selectNext = false;

        foreach (string title in orderedTitles)
        {
            if (title.Equals(_activeClientTitle, StringComparison.Ordinal)
                && !title.Equals(DefaultClientTitle, StringComparison.Ordinal))
            {
                selectNext = true;
                continue;
            }

            if (title.Equals(_activeClientTitle, StringComparison.Ordinal)
                && title.Equals(DefaultClientTitle, StringComparison.Ordinal))
            {
                if (TryGetNextLoginClient(
                        forward,
                        out IThumbnailView? loginClient,
                        out bool activeLoginIsEligible))
                {
                    SetActive(loginClient!);
                    return;
                }

                // The active login client was the last instance. The reference
                // implementation continues into the next configured title.
                selectNext = activeLoginIsEligible;
                continue;
            }

            if (!selectNext)
            {
                continue;
            }

            if (TryGetCycleTarget(title, forward, out IThumbnailView? target))
            {
                Logger.Debug("ThumbnailManager", $"Cycle target: {target!.ProcessName}::{target.Title}");
                SetActive(target!);
                return;
            }
        }

        // No later client was available, so wrap back to the first eligible one.
        foreach (string title in orderedTitles)
        {
            if (TryGetCycleTarget(title, forward, out IThumbnailView? target))
            {
                Logger.Debug("ThumbnailManager", $"Cycle wrapped target: {target!.ProcessName}::{target.Title}");
                SetActive(target!);
                return;
            }
        }
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

    private List<string> ResolveCycleOrder(WindowGroupConfig group)
    {
        var titles = new List<string>();
        foreach (string configuredKey in group.WindowKeys)
        {
            string title = GetWindowTitle(configuredKey);
            if (!string.IsNullOrWhiteSpace(title)
                && !titles.Contains(title, StringComparer.Ordinal))
            {
                titles.Add(title);
            }
        }

        if (titles.Count > 0)
        {
            return titles;
        }

        // Empty order means cycle all known client titles in their current view order.
        foreach (IThumbnailView view in _thumbnailViews.Values)
        {
            if (!titles.Contains(view.Title, StringComparer.Ordinal))
            {
                titles.Add(view.Title);
            }
        }

        return titles;
    }

    private bool TryGetNextLoginClient(
        bool forward,
        out IThumbnailView? nextClient,
        out bool activeClientIsEligible)
    {
        nextClient = null;
        activeClientIsEligible = false;
        IEnumerable<IThumbnailView> candidates = GetCycleCandidates(DefaultClientTitle, forward);
        foreach (IThumbnailView candidate in candidates)
        {
            if (candidate.Id == _activeClient)
            {
                activeClientIsEligible = true;
                continue;
            }

            if (activeClientIsEligible)
            {
                nextClient = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetCycleTarget(string title, bool forward, out IThumbnailView? target)
    {
        target = GetCycleCandidates(title, forward).FirstOrDefault();
        return target != null;
    }

    private IEnumerable<IThumbnailView> GetCycleCandidates(string title, bool forward)
    {
        IEnumerable<IThumbnailView> candidates = _thumbnailViews.Values.Where(view =>
            view.Title.Equals(title, StringComparison.Ordinal)
            && !view.IsExcludedFromCycleGroup);

        return title.Equals(DefaultClientTitle, StringComparison.Ordinal)
            ? (forward
                ? candidates.OrderBy(view => view.Id.ToInt64())
                : candidates.OrderByDescending(view => view.Id.ToInt64()))
            : candidates;
    }

    private void SwitchActiveClient(IntPtr foregroundClientHandle, string foregroundClientTitle)
    {
        if (_activeClient == foregroundClientHandle)
        {
            _activeClientTitle = foregroundClientTitle;
            return;
        }

        _activeClient = foregroundClientHandle;
        _activeClientTitle = foregroundClientTitle;
    }

    private static string GetWindowTitle(string windowKey)
    {
        int separatorIndex = windowKey.IndexOf("::", StringComparison.Ordinal);
        return separatorIndex < 0 ? windowKey : windowKey[(separatorIndex + 2)..];
    }

    private void HotkeyPressed(int groupIndex, bool forward)
    {
        if (_dispatcher.HasThreadAccess)
        {
            CycleGroup(groupIndex, forward);
            return;
        }

        if (!_dispatcher.TryEnqueue(() => CycleGroup(groupIndex, forward)))
        {
            Logger.Warn("ThumbnailManager", "Unable to enqueue group hotkey switch");
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
            _activeClientTitle = DefaultClientTitle;
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
