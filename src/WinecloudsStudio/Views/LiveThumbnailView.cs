using System.Drawing;
using WinecloudsStudio.Services.Interface;

namespace WinecloudsStudio.Views;

public sealed class LiveThumbnailView : ThumbnailView
{
    private IDwmThumbnail? _thumbnail;
    private Rectangle _destination;

    public LiveThumbnailView(
        IWindowManager windowManager,
        IntPtr id,
        string processName,
        string title,
        Size size,
        Point location)
        : base(windowManager, id, processName, title, size, location)
    {
        _destination = new Rectangle(Point.Empty, size);
    }

    protected override void RefreshLiveThumbnail(bool forceRefresh)
    {
        IDwmThumbnail? obsoleteThumbnail = forceRefresh ? _thumbnail : null;

        if (_thumbnail == null || forceRefresh)
        {
            var replacement = WindowManager.GetLiveThumbnail(Handle, Id);
            replacement.Move(
                _destination.Left,
                _destination.Top,
                _destination.Right,
                _destination.Bottom);
            replacement.Update();
            _thumbnail = replacement;
        }

        if (obsoleteThumbnail != null && !ReferenceEquals(obsoleteThumbnail, _thumbnail))
        {
            obsoleteThumbnail.Unregister();
        }
    }

    protected override void ResizeLiveThumbnail(Rectangle destination)
    {
        if (_destination == destination)
        {
            return;
        }

        _destination = destination;
        _thumbnail?.Move(destination.Left, destination.Top, destination.Right, destination.Bottom);
        _thumbnail?.Update();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnail?.Unregister();
            _thumbnail = null;
        }

        base.Dispose(disposing);
    }
}
