using System.Drawing;

namespace WinecloudsStudio.Modules.WindowManager.Views;

public interface IThumbnailView : IDisposable
{
    IntPtr Id { get; }
    IntPtr Handle { get; }
    string ProcessName { get; }
    string Title { get; set; }
    Point ThumbnailLocation { get; set; }
    Size ThumbnailSize { get; set; }
    bool IsActive { get; }

    event Action<IntPtr>? ThumbnailActivated;
    event Action<IntPtr>? ThumbnailMoved;

    bool IsKnownHandle(IntPtr handle);
    void ShowThumbnail();
    void RefreshThumbnail(bool forceRefresh);
    void SetOpacity(double opacity);
    void SetTopMost(bool topMost);
    void SetPositionLocked(bool locked);
    void SetSnapToGrid(bool snapToGrid);
    void SetHighlight(bool highlighted);
    void SetShowBorder(bool showBorder);
    void SetOverlayLabel(bool showLabel);
}
