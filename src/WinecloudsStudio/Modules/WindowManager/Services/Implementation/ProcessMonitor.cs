using System.Diagnostics;
using WinecloudsStudio.Modules.WindowManager.Services.Interface;

namespace WinecloudsStudio.Modules.WindowManager.Services.Implementation;

/// <summary>
/// Monitors running processes and detects added/updated/removed windows.
/// Adapted from EVE-O-Preview with support for configurable process names.
/// </summary>
public class ProcessMonitor : IProcessMonitor
{
    private readonly IDictionary<IntPtr, string> _processCache;
    private readonly HashSet<string> _monitoredProcesses;
    private IProcessInfo _currentProcessInfo;

    public ProcessMonitor()
    {
        _processCache = new Dictionary<IntPtr, string>(512);
        _monitoredProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _currentProcessInfo = new ProcessInfo(IntPtr.Zero, "");
    }

    public void AddMonitoredProcess(string processName)
    {
        _monitoredProcesses.Add(processName);
    }

    public void RemoveMonitoredProcess(string processName)
    {
        _monitoredProcesses.Remove(processName);
    }

    public void ClearMonitoredProcesses()
    {
        _monitoredProcesses.Clear();
        _processCache.Clear();
    }

    public IReadOnlyList<string> GetMonitoredProcesses()
    {
        return _monitoredProcesses.ToList().AsReadOnly();
    }

    public IProcessInfo GetMainProcess()
    {
        if (_currentProcessInfo.Handle == IntPtr.Zero)
        {
            var currentProcess = Process.GetCurrentProcess();
            _currentProcessInfo = new ProcessInfo(currentProcess.MainWindowHandle, currentProcess.MainWindowTitle);
        }

        return _currentProcessInfo;
    }

    public ICollection<IProcessInfo> GetAllProcesses()
    {
        var result = new List<IProcessInfo>(_processCache.Count);

        foreach (var entry in _processCache)
        {
            result.Add(new ProcessInfo(entry.Key, entry.Value));
        }

        return result;
    }

    public void GetUpdatedProcesses(
        out ICollection<IProcessInfo> addedProcesses,
        out ICollection<IProcessInfo> updatedProcesses,
        out ICollection<IProcessInfo> removedProcesses)
    {
        addedProcesses = new List<IProcessInfo>(16);
        updatedProcesses = new List<IProcessInfo>(16);
        removedProcesses = new List<IProcessInfo>(16);

        var knownProcesses = new List<IntPtr>(_processCache.Keys);

        foreach (var process in Process.GetProcesses())
        {
            string processName = process.ProcessName;

            if (!_monitoredProcesses.Contains(processName))
            {
                continue;
            }

            IntPtr mainWindowHandle = process.MainWindowHandle;
            if (mainWindowHandle == IntPtr.Zero)
            {
                continue;
            }

            string mainWindowTitle = process.MainWindowTitle;

            _processCache.TryGetValue(mainWindowHandle, out string? cachedTitle);

            if (cachedTitle == null)
            {
                // New process
                _processCache.Add(mainWindowHandle, mainWindowTitle);
                addedProcesses.Add(new ProcessInfo(mainWindowHandle, mainWindowTitle));
            }
            else
            {
                // Known process — check if title changed
                if (cachedTitle != mainWindowTitle)
                {
                    _processCache[mainWindowHandle] = mainWindowTitle;
                    updatedProcesses.Add(new ProcessInfo(mainWindowHandle, mainWindowTitle));
                }

                knownProcesses.Remove(mainWindowHandle);
            }
        }

        // Items still in knownProcesses are gone
        foreach (var index in knownProcesses)
        {
            string title = _processCache[index];
            removedProcesses.Add(new ProcessInfo(index, title));
            _processCache.Remove(index);
        }
    }
}
