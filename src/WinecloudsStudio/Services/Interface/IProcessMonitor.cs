namespace WinecloudsStudio.Services.Interface;

/// <summary>
/// Monitors specific processes and reports added/updated/removed windows.
/// </summary>
public interface IProcessMonitor
{
    IProcessInfo GetMainProcess();
    ICollection<IProcessInfo> GetAllProcesses();
    void GetUpdatedProcesses(
        out ICollection<IProcessInfo> addedProcesses,
        out ICollection<IProcessInfo> updatedProcesses,
        out ICollection<IProcessInfo> removedProcesses);
    void AddMonitoredProcess(string processName);
    void RemoveMonitoredProcess(string processName);
    void ClearMonitoredProcesses();
    IReadOnlyList<string> GetMonitoredProcesses();
}
