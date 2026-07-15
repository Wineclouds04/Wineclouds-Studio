using WinecloudsStudio.Modules.WindowManager.Services.Interface;

namespace WinecloudsStudio.Modules.WindowManager.Services.Implementation;

/// <summary>
/// Simple DTO holding a window handle and its title.
/// </summary>
public class ProcessInfo : IProcessInfo
{
    public ProcessInfo(IntPtr handle, string title)
    {
        Handle = handle;
        Title = title;
    }

    public IntPtr Handle { get; }
    public string Title { get; }
}
