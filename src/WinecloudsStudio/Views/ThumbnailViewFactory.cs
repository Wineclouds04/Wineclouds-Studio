using System.Drawing;
using WinecloudsStudio.Services.Interface;

namespace WinecloudsStudio.Views;

public sealed class ThumbnailViewFactory
{
    private readonly IWindowManager _windowManager;

    public ThumbnailViewFactory(IWindowManager windowManager)
    {
        _windowManager = windowManager;
    }

    public IThumbnailView Create(
        IntPtr id,
        string processName,
        string title,
        Size size,
        Point location)
    {
        return new LiveThumbnailView(_windowManager, id, processName, title, size, location);
    }
}
