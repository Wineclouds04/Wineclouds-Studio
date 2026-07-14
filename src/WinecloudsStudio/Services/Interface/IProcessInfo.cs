namespace WinecloudsStudio.Services.Interface;

/// <summary>
/// Simple DTO for process information.
/// </summary>
public interface IProcessInfo
{
    IntPtr Handle { get; }
    string Title { get; }
}
